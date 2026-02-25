using System.Collections.Generic;
using System.Linq;

public class CompetitionRuntimeState
{
    [System.NonSerialized]
    public CompetitionSO competition;

    public string competitionID;
    public int currentRaceIndex;
    public List<TeamPointsEntry> teamPointsList = new();
    public List<RacePointsEntry> raceResultsList = new();

    [System.NonSerialized] public Dictionary<TeamSO, float> teamPoints = new();
    [System.NonSerialized] public Dictionary<int, Dictionary<TeamSO, float>> raceResults = new();

    public EliminationHistory eliminationHistory = new EliminationHistory();

    public List<PlayerSessionSaveData> savedPlayers = new();
    public bool savedIsSequenceRace;
    
    // Propriedade que retorna times eliminados ATÉ a corrida ATUAL
    public HashSet<TeamSO> eliminatedTeams 
    { 
        get 
        { 
            return new HashSet<TeamSO>(eliminationHistory.GetTeamsEliminatedByRace(currentRaceIndex - 1));
        } 
    }
    
    public bool IsFinal => currentRaceIndex == competition.circuits.Count - 1;

    public void BuildDictionaries()
    {
        teamPoints = new Dictionary<TeamSO, float>();
        foreach (var entry in teamPointsList)
            teamPoints[entry.team] = entry.points;

        raceResults = new Dictionary<int, Dictionary<TeamSO, float>>();
        foreach (var race in raceResultsList)
        {
            var dict = new Dictionary<TeamSO, float>();
            foreach (var entry in race.teamPoints)
                dict[entry.team] = entry.points;

            raceResults[race.raceIndex] = dict;
        }
    }

    public void SyncListsFromDictionaries()
    {
        teamPointsList = new List<TeamPointsEntry>();
        foreach (var kvp in teamPoints)
        {
            teamPointsList.Add(new TeamPointsEntry
            {
                team = kvp.Key,
                points = kvp.Value
            });
        }

        raceResultsList = new List<RacePointsEntry>();
        foreach (var race in raceResults)
        {
            RacePointsEntry raceEntry = new RacePointsEntry
            {
                raceIndex = race.Key
            };

            foreach (var kvp in race.Value)
            {
                raceEntry.teamPoints.Add(new TeamPointsEntry
                {
                    team = kvp.Key,
                    points = kvp.Value
                });
            }

            raceResultsList.Add(raceEntry);
        }
    }
}

[System.Serializable]
public class EliminationHistory
{
    [System.Serializable]
    public class EliminationEntry
    {
        public int raceIndex;
        public TeamSO team;
    }

    //ISSO é o que será salvo
    public List<EliminationEntry> eliminationsList = new();

    // runtime only
    [System.NonSerialized]
    public Dictionary<int, List<TeamSO>> eliminationsByRace = new();

    [System.NonSerialized]
    private Dictionary<TeamSO, int> _eliminationRaceCache = new();

    // ----------------------------------

    public void RegisterElimination(int raceIndex, TeamSO team)
    {
        eliminationsList.Add(new EliminationEntry
        {
            raceIndex = raceIndex,
            team = team
        });

        if (!eliminationsByRace.ContainsKey(raceIndex))
            eliminationsByRace[raceIndex] = new List<TeamSO>();

        eliminationsByRace[raceIndex].Add(team);
        _eliminationRaceCache[team] = raceIndex;
    }

    public void ClearEliminationsAfter(int raceIndex)
    {
        eliminationsList.RemoveAll(e => e.raceIndex > raceIndex);
        BuildRuntimeDictionaries();
    }

    public void BuildRuntimeDictionaries()
    {
        eliminationsByRace = new Dictionary<int, List<TeamSO>>();
        _eliminationRaceCache = new Dictionary<TeamSO, int>();

        foreach (var e in eliminationsList)
        {
            if (!eliminationsByRace.ContainsKey(e.raceIndex))
                eliminationsByRace[e.raceIndex] = new List<TeamSO>();

            eliminationsByRace[e.raceIndex].Add(e.team);
            _eliminationRaceCache[e.team] = e.raceIndex;
        }
    }

    public int GetEliminationRace(TeamSO team)
    {
        return _eliminationRaceCache.ContainsKey(team)
            ? _eliminationRaceCache[team]
            : -1;
    }

    public bool IsTeamEliminatedByRace(int raceIndex, TeamSO team)
    {
        int elimRace = GetEliminationRace(team);
        return elimRace != -1 && elimRace <= raceIndex;
    }

    public List<TeamSO> GetTeamsEliminatedByRace(int raceIndex)
    {
        return eliminationsList
            .Where(e => e.raceIndex <= raceIndex)
            .Select(e => e.team)
            .Distinct()
            .ToList();
    }
}