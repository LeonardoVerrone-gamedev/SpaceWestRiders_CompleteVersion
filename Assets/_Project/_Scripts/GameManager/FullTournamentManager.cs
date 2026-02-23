using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;

public class FullTournamentManager : MonoBehaviour
{
    public static FullTournamentManager Instance;

    public CompetitionRuntimeState CurrentState { get; private set; }

    private string SavePath => Path.Combine(Application.persistentDataPath, "full_tournament_save.json");

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            //LoadIfExists();
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

    #region START

    public void StartTournament(CompetitionSO competition)
    {
        SCR_PersistentData.Instance?.ResetSession();

        CurrentState = new CompetitionRuntimeState();
        CurrentState.competition = competition;
        CurrentState.currentRaceIndex = 0;

        foreach (var team in competition.teams)
            CurrentState.teamPoints[team] = 0;

        Save();

        SceneManager.LoadScene("TournamentMainMenu");
    }

    public void LoadOldTournament()
    {
        LoadIfExists();
        //SCR_PersistentData.Instance?.ResetSession();
        SceneManager.LoadScene("TournamentMainMenu");
    }

    #endregion

    #region RACE FLOW

    public void LoadRace(int raceIndex)
    {
        if (CurrentState == null) return;

        if (!IsRaceUnlocked(raceIndex))
            return;

        CurrentState.currentRaceIndex = raceIndex;
        Save();

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
        Save();
        LoadRace(CurrentState.currentRaceIndex);
    }

    #endregion

    #region RESULTS

    public void ProcessRaceResults(List<RaceResultData> results)
    {
        int raceIndex = CurrentState.currentRaceIndex;

        if (CurrentState.raceResults.ContainsKey(raceIndex))
            RevertRacePoints(raceIndex);

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

        Save();
    }

    private void RevertRacePoints(int raceIndex)
    {
        var oldRace = CurrentState.raceResults[raceIndex];

        foreach (var kvp in oldRace)
            CurrentState.teamPoints[kvp.Key] -= kvp.Value;

        CurrentState.raceResults.Remove(raceIndex);
        CurrentState.eliminationHistory.ClearEliminationsAfter(raceIndex - 1);
    }

    #endregion

    #region ELIMINATION (MESMA LÓGICA)

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
        float lowestPoints = float.MaxValue;

        var teamsAlive = CurrentState.teamPoints.Keys
            .Where(t => !CurrentState.eliminationHistory
            .IsTeamEliminatedByRace(CurrentState.currentRaceIndex, t))
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
                .RegisterElimination(CurrentState.currentRaceIndex, lowestTeam);
    }

    #endregion

    #region UNLOCK

    public bool IsRaceUnlocked(int raceIndex)
    {
        if (CurrentState.raceResults.ContainsKey(raceIndex))
            return true;

        if (raceIndex == 0) return true;

        if (!CurrentState.raceResults.ContainsKey(raceIndex - 1))
            return false;

        bool anyAlive = SCR_PersistentData.Instance.players
            .Any(p => !CurrentState.eliminationHistory
            .IsTeamEliminatedByRace(raceIndex - 1, p.selectedCarData.team));

        return anyAlive;
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

    #region SAVE SYSTEM

    private void Save()
    {
        if (CurrentState == null) return;

        SaveSessionData();

        CurrentState.SyncListsFromDictionaries();

        string json = JsonUtility.ToJson(CurrentState, true);
        File.WriteAllText(SavePath, json);
    }

    private void SaveSessionData()
    {
        CurrentState.savedPlayers.Clear();

        if (SCR_PersistentData.Instance == null)
            return;

        foreach (var p in SCR_PersistentData.Instance.players)
        {
            CurrentState.savedPlayers.Add(new PlayerSessionSaveData
            {
                playerIndex = p.playerIndex,
                selectedCarGridIndex = p.selectedCarGridIndex,
                selectedCharacterID = p.selectedCharacterID,
                hasConfirmed = p.hasConfirmed
            });
        }

        CurrentState.savedIsSequenceRace = SCR_PersistentData.Instance.isSequenceRace;
    }


    public void LoadIfExists()
    {
        if (!CheckFile()) return;

        string json = File.ReadAllText(SavePath);
        CurrentState = JsonUtility.FromJson<CompetitionRuntimeState>(json);

        //GARANTE QUE LISTAS NÃO VENHAM NULL
        if (CurrentState.savedPlayers == null)
            CurrentState.savedPlayers = new List<PlayerSessionSaveData>();

        if (CurrentState.raceResults == null)
            CurrentState.raceResults = new Dictionary<int, Dictionary<TeamSO, float>>();

        if (CurrentState.teamPoints == null)
            CurrentState.teamPoints = new Dictionary<TeamSO, float>();

        if (CurrentState.eliminationHistory == null)
            CurrentState.eliminationHistory = new EliminationHistory();

        RestoreSessionData();

        CurrentState.BuildDictionaries();
        CurrentState.eliminationHistory.BuildRuntimeDictionaries();
    }

    private void RestoreSessionData()
    {
        if (CurrentState == null) return;

        if (SCR_PersistentData.Instance == null)
            Instantiate(new GameObject("PersistentData")
                .AddComponent<SCR_PersistentData>());

        if (SCR_PersistentData.Instance.players == null)
            SCR_PersistentData.Instance.players = new List<PlayerSessionData>();
        else
            SCR_PersistentData.Instance.players.Clear();

        if (CurrentState.savedPlayers == null)
            return;

        foreach (var saved in CurrentState.savedPlayers)
        {
            var session = new PlayerSessionData
            {
                playerIndex = saved.playerIndex,
                selectedCarGridIndex = saved.selectedCarGridIndex,
                selectedCharacterID = saved.selectedCharacterID,
                hasConfirmed = false
            };

            session.selectedCarData =
                FindRacerProfileByID(saved.selectedCharacterID);

            SCR_PersistentData.Instance.players.Add(session);
        }

        SCR_PersistentData.Instance.isSequenceRace =
            CurrentState.savedIsSequenceRace;
    }

    private RacerProfileSO FindRacerProfileByID(string id)
    {
        foreach (var team in CurrentState.competition.teams)
            foreach (var racer in team.racers)
                if (racer.racerName == id)
                    return racer;

        return null;
    }


    public bool CheckFile()
    {
        if (File.Exists(SavePath)) return true;

        return false;
    }

    public void DeleteSave()
    {
        if (File.Exists(SavePath))
            File.Delete(SavePath);
    }

    #endregion

    #region HELPERS

    private RacerProfileSO FindRacerProfileByName(string racerName)
    {
        foreach (var team in CurrentState.competition.teams)
            foreach (var racer in team.racers)
                if (racer.racerName == racerName)
                    return racer;

        return null;
    }

    public void EndTournament()
    {
        DeleteSave();
        CurrentState = null;
        SceneManager.LoadScene("TitleScreen");
    }

    public void SaveAndQuit()
    {
        Save();
        SceneManager.LoadScene("TitleScreen");
    }

    #endregion
}
