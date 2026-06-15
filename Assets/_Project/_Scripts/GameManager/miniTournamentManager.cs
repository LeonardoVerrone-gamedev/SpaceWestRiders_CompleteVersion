using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MiniTournamentManager : MonoBehaviour
{
    public static MiniTournamentManager Instance;

    public CompetitionRuntimeState CurrentState { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "TitleScreen")
        {
            Destroy(gameObject);
        }
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    #region START TOURNAMENT

    public void StartTournament(CompetitionSO competition)
    {
        SCR_PersistentData.Instance?.ResetSession();

        CurrentState = new CompetitionRuntimeState();
        CurrentState.competition = competition;
        CurrentState.currentRaceIndex = 0;

        foreach (var team in competition.teams)
        {
            CurrentState.teamPoints[team] = 0;
        }

        SceneTransitionAnimationManager.Instance.LoadScene("MiniTournamentMainMenu");
    }

    #endregion

    #region RACE FLOW

    public void LoadRace(int raceIndex)
    {
        if (CurrentState == null) return;

        if (!IsRaceUnlocked(raceIndex)) 
        {
            Debug.Log($"Corrida {raceIndex} não está liberada");
            return;
        }

        CurrentState.currentRaceIndex = raceIndex;

        var circuit = CurrentState.competition.circuits[raceIndex];
        SceneTransitionAnimationManager.Instance.LoadScene(circuit.sceneName);
    }

    public bool HasNextRace()
    {
        return CurrentState.currentRaceIndex < CurrentState.competition.circuits.Count - 1;
    }

    public void AdvanceToNextRace()
    {
        if (!HasNextRace()) return;

        CurrentState.currentRaceIndex++;
        LoadRace(CurrentState.currentRaceIndex);
    }

    #endregion

    #region PROCESS RESULTS

    public void ProcessRaceResults(List<RaceResultData> results)
    {
        int raceIndex = CurrentState.currentRaceIndex;

        // Redisputa → remover pontos antigos
        if (CurrentState.raceResults.ContainsKey(raceIndex))
        {
            RevertRacePoints(raceIndex);
        }

        Dictionary<TeamSO, float> racePoints = new();

        foreach (var r in results)
        {
            var profile = FindRacerProfileByName(r.racerName);
            if (profile == null) continue;

            TeamSO team = profile.team;

            if (!racePoints.ContainsKey(team))
                racePoints[team] = 0;

            racePoints[team] += r.points;
            CurrentState.teamPoints[team] += r.points;
        }

        CurrentState.raceResults[raceIndex] = racePoints;

        RecalculateEliminations();

        if (CurrentState.IsFinal)
        {
            // Pega o primeiro colocado da última corrida
            var firstPlace = results.OrderBy(r => r.position).FirstOrDefault();
            
            if (firstPlace != null && firstPlace.isPlayer)
            {
                Debug.Log("MINI TORNEIO CONCLUIDO! Vencedor: {firstPlace.racerName}");
            }
        }//
    }

    private void RevertRacePoints(int raceIndex)
    {
        var oldRace = CurrentState.raceResults[raceIndex];

        foreach (var kvp in oldRace)
        {
            CurrentState.teamPoints[kvp.Key] -= kvp.Value;
        }

        CurrentState.raceResults.Remove(raceIndex);
        
        // Remove eliminações que ocorreram após esta corrida
        CurrentState.eliminationHistory.ClearEliminationsAfter(raceIndex - 1);
    }

    #endregion

    #region ELIMINATION

    private void RecalculateEliminations()
    {
        if (CurrentState?.competition?.EliminateAt == null)
            return;

        // Limpa TODAS as eliminações
        CurrentState.eliminationHistory.ClearEliminationsAfter(-1);

        //  Recalcula em ordem cronológica
        var eliminateAt = CurrentState.competition.EliminateAt.OrderBy(x => x);

        foreach (int checkpoint in eliminateAt)
        {
            // Só processa se essa corrida já foi jogada
            if (!CurrentState.raceResults.ContainsKey(checkpoint))
                continue;

            EliminateLowestAtCheckpoint(checkpoint);
        }
    }

    private void EliminateLowestAtCheckpoint(int raceIndex)
    {
        TeamSO lowestTeam = null;
        float lowestPoints = float.MaxValue;

        var teamsAlive = CurrentState.teamPoints.Keys
            .Where(t => !CurrentState.eliminationHistory
                .IsTeamEliminatedByRace(raceIndex - 1, t))
            .ToList();

        foreach (var team in teamsAlive)
        {
            if (CurrentState.teamPoints[team] < lowestPoints)
            {
                lowestPoints = CurrentState.teamPoints[team];
                lowestTeam = team;
            }
        }

        if (lowestTeam != null)
            CurrentState.eliminationHistory
                .RegisterElimination(raceIndex, lowestTeam);
    }

    private void EliminateLowest()
    {
        TeamSO lowestTeam = null;
        float lowestPoints = int.MaxValue;

        // Times que ainda NÃO foram eliminados até a corrida ATUAL
        var teamsAlive = CurrentState.teamPoints.Keys
            .Where(t => !CurrentState.eliminationHistory.IsTeamEliminatedByRace(CurrentState.currentRaceIndex, t))
            .ToList();

        foreach (var team in teamsAlive)
        {
            if (CurrentState.teamPoints[team] < lowestPoints)
            {
                lowestPoints = CurrentState.teamPoints[team];
                lowestTeam = team;
            }
        }

        if (lowestTeam != null)
        {
            CurrentState.eliminationHistory.RegisterElimination(CurrentState.currentRaceIndex, lowestTeam);
            Debug.Log($"Equipe {lowestTeam.teamName} eliminada na corrida {CurrentState.currentRaceIndex + 1} com {lowestPoints} pontos");
        }
    }

    public List<TeamSO> GetRemainingTeams()
    {
        return CurrentState.teamPoints.Keys
            .Where(t => !CurrentState.eliminationHistory.IsTeamEliminatedByRace(CurrentState.currentRaceIndex, t))
            .ToList();
    }

    public bool IsFinalRace()
    {
        return CurrentState.raceResults.Count >= 3;
    }

    #endregion

    #region UNLOCK LOGIC (CORRIGIDA)

    public bool IsRaceUnlocked(int raceIndex)
    {
        // Corridas já disputadas estão SEMPRE liberadas
        if (CurrentState.raceResults.ContainsKey(raceIndex))
            return true;

        // Primeira corrida não disputada ainda
        if (raceIndex == 0) return true;

        // Precisa ter disputado a anterior
        if (!CurrentState.raceResults.ContainsKey(raceIndex - 1))
            return false;

        // Para corridas futuras, precisa ter pelo menos um jogador vivo
        bool anyPlayerAlive = SCR_PersistentData.Instance.players
            .Any(p => !CurrentState.eliminationHistory.IsTeamEliminatedByRace(raceIndex - 1, p.selectedCarData.team));

        return anyPlayerAlive;
    }

    #endregion

    #region Kick P2

    public void KickP2()
    {
        if (SCR_PersistentData.Instance == null || SCR_PersistentData.Instance.players.Count < 2)
            return;

        SCR_PersistentData.Instance.players.Remove(SCR_PersistentData.Instance.players[1]);
    }

    #endregion

    #region HELPERS

    private RacerProfileSO FindRacerProfileByName(string racerName)
    {
        foreach (var team in CurrentState.competition.teams)
        {
            foreach (var racer in team.racers)
            {
                if (racer.racerName == racerName)
                    return racer;
            }
        }
        return null;
    }

    public bool IsTeamEliminated(TeamSO team)
    {
        return CurrentState.eliminationHistory.IsTeamEliminatedByRace(CurrentState.currentRaceIndex - 1, team);
    }

    #endregion

    #region END TOURNAMENT

    public void EndTournament()
    {
        CurrentState = null;
        SceneTransitionAnimationManager.Instance.LoadScene("TitleScreen");
    }

    #endregion
}