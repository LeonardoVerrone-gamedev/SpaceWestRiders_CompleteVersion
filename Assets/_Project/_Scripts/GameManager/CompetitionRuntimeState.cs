using System.Collections.Generic;
using System.Linq;

public class CompetitionRuntimeState
{
    public CompetitionSO competition;
    public int currentRaceIndex;
    public Dictionary<TeamSO, float> teamPoints = new();
    public Dictionary<int, Dictionary<TeamSO, float>> raceResults = new();
    public EliminationHistory eliminationHistory = new EliminationHistory(); // NOVO
    
    // Propriedade que retorna times eliminados ATÉ a corrida ATUAL
    public HashSet<TeamSO> eliminatedTeams 
    { 
        get 
        { 
            return new HashSet<TeamSO>(eliminationHistory.GetTeamsEliminatedByRace(currentRaceIndex - 1));
        } 
    }
    
    public bool IsFinal => currentRaceIndex == competition.circuits.Count - 1;
}

[System.Serializable]
public class EliminationHistory
{
    // Dicionário: corrida -> times eliminados NESSA corrida
    public Dictionary<int, List<TeamSO>> eliminationsByRace = new Dictionary<int, List<TeamSO>>();
    
    // Cache para acesso rápido: time -> corrida em que foi eliminado
    private Dictionary<TeamSO, int> _eliminationRaceCache = new Dictionary<TeamSO, int>();
    
    public void RegisterElimination(int raceIndex, TeamSO team)
    {
        if (!eliminationsByRace.ContainsKey(raceIndex))
            eliminationsByRace[raceIndex] = new List<TeamSO>();
            
        if (!eliminationsByRace[raceIndex].Contains(team))
        {
            eliminationsByRace[raceIndex].Add(team);
            _eliminationRaceCache[team] = raceIndex;
        }
    }
    
    public void ClearEliminationsAfter(int raceIndex)
    {
        // Remove eliminações de corridas posteriores
        var racesToRemove = eliminationsByRace.Keys.Where(r => r > raceIndex).ToList();
        foreach (var race in racesToRemove)
        {
            foreach (var team in eliminationsByRace[race])
            {
                _eliminationRaceCache.Remove(team);
            }
            eliminationsByRace.Remove(race);
        }
    }
    
    public int GetEliminationRace(TeamSO team)
    {
        return _eliminationRaceCache.ContainsKey(team) ? _eliminationRaceCache[team] : -1;
    }
    
    public bool IsTeamEliminatedByRace(int raceIndex, TeamSO team)
    {
        int elimRace = GetEliminationRace(team);
        return elimRace != -1 && elimRace <= raceIndex;
    }
    
    public List<TeamSO> GetTeamsEliminatedByRace(int raceIndex)
    {
        return eliminationsByRace
            .Where(kvp => kvp.Key <= raceIndex)
            .SelectMany(kvp => kvp.Value)
            .Distinct()
            .ToList();
    }
}
