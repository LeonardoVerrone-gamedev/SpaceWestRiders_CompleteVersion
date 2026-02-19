using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TournamentFullManager : MonoBehaviour
{
    public static TournamentFullManager Instance;

    public CompetitionRuntimeState CurrentState { get; private set; }
    
    [Header("Tournament Settings")]
    [SerializeField] private int[] eliminationRaces = new int[] { 7, 15, 18 }; // 8ª, 16ª e 19ª corridas (0-based)

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

        GameManagerInstance.Instance.SetGameMode(GameMode.Tournament);

        CurrentState = new CompetitionRuntimeState();
        CurrentState.competition = competition;
        CurrentState.currentRaceIndex = 0;

        foreach (var team in competition.teams)
        {
            CurrentState.teamPoints[team] = 0;
        }

        SceneManager.LoadScene("TournamentFullMainMenu");
    }

    #endregion

    #region RACE FLOW

    public void LoadRace(int raceIndex)
    {
        if (CurrentState == null) return;

        if (!IsRaceUnlocked(raceIndex)) return;

        CurrentState.currentRaceIndex = raceIndex;

        var circuit = CurrentState.competition.circuits[raceIndex];
        SceneManager.LoadScene(circuit.sceneName);
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

        Dictionary<TeamSO, int> racePoints = new();

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
    }

    private void RevertRacePoints(int raceIndex)
    {
        var oldRace = CurrentState.raceResults[raceIndex];

        foreach (var kvp in oldRace)
        {
            CurrentState.teamPoints[kvp.Key] -= kvp.Value;
        }

        CurrentState.raceResults.Remove(raceIndex);
    }

    #endregion

    #region ELIMINATION

    private void RecalculateEliminations()
    {
        // Guarda as eliminações anteriores para não perder o histórico
        HashSet<TeamSO> previousEliminations = new HashSet<TeamSO>(CurrentState.eliminatedTeams);
        CurrentState.eliminatedTeams.Clear();

        int racesCompleted = CurrentState.raceResults.Count;

        // Aplica as eliminações apenas nas corridas específicas
        foreach (int raceToEliminate in eliminationRaces)
        {
            if (racesCompleted > raceToEliminate) // Já passou da corrida de eliminação
            {
                EliminateLowest();
            }
        }

        // Garante que times já eliminados permaneçam eliminados
        foreach (var team in previousEliminations)
        {
            if (!CurrentState.eliminatedTeams.Contains(team))
            {
                CurrentState.eliminatedTeams.Add(team);
            }
        }
    }

    private void EliminateLowest()
    {
        TeamSO lowestTeam = null;
        int lowestPoints = int.MaxValue;

        foreach (var kvp in CurrentState.teamPoints)
        {
            if (CurrentState.eliminatedTeams.Contains(kvp.Key))
                continue;

            if (kvp.Value < lowestPoints)
            {
                lowestPoints = kvp.Value;
                lowestTeam = kvp.Key;
            }
        }

        if (lowestTeam != null)
        {
            CurrentState.eliminatedTeams.Add(lowestTeam);
            Debug.Log($"Equipe eliminada: {lowestTeam.teamName} com {lowestPoints} pontos");
        }
    }

    public List<TeamSO> GetRemainingTeams()
    {
        return CurrentState.teamPoints
            .Where(kvp => !CurrentState.eliminatedTeams.Contains(kvp.Key))
            .Select(kvp => kvp.Key)
            .ToList();
    }

    public bool IsFinalRace()
    {
        return CurrentState.raceResults.Count >= CurrentState.competition.circuits.Count;
    }

    #endregion

    #region UNLOCK LOGIC

    public bool IsRaceUnlocked(int raceIndex)
    {
        if (raceIndex == 0) return true;

        bool previousPlayed = CurrentState.raceResults.ContainsKey(raceIndex - 1);
        bool anyPlayerAlive = SCR_PersistentData.Instance.players
            .Any(p => !CurrentState.eliminatedTeams.Contains(p.selectedCarData.team));

        return previousPlayed && anyPlayerAlive;
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
        return CurrentState.eliminatedTeams.Contains(team);
    }

    #endregion

    #region END TOURNAMENT

    public void EndTournament()
    {
        CurrentState = null;
        GameManagerInstance.Instance.SetGameMode(GameMode.None);
        SceneManager.LoadScene("TitleScreen");
    }

    #endregion
}