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

    [System.Serializable]
    public class RaceFinalRankingEntry
    {
        public int raceIndex;
        public List<RaceResultData> results = new();
    }

    // SERIALIZADO
    public List<RaceFinalRankingEntry> finalRankingsList = new();

    // RUNTIME
    [System.NonSerialized]
    public Dictionary<int, List<RaceResultData>> finalRankings = new();

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

        // FINAL RANKINGS
        finalRankings = new Dictionary<int, List<RaceResultData>>();

        foreach (var entry in finalRankingsList)
        {
            finalRankings[entry.raceIndex] = new List<RaceResultData>(entry.results);
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

        // FINAL RANKINGS
        finalRankingsList = new List<RaceFinalRankingEntry>();

        foreach (var kvp in finalRankings)
        {
            finalRankingsList.Add(new RaceFinalRankingEntry
            {
                raceIndex = kvp.Key,
                results = new List<RaceResultData>(kvp.Value)
            });
        }
    }

    public List<RaceResultData> GetRaceResults(int raceIndex)
    {
        if (finalRankings == null)
            BuildDictionaries();

        if (finalRankings.ContainsKey(raceIndex))
            return finalRankings[raceIndex];

        return null;
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