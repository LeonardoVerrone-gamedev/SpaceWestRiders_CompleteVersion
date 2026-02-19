using System.Collections.Generic;

public class CompetitionRuntimeState
{
    public CompetitionSO competition;

    public int currentRaceIndex;

    // Pontuação por equipe
    public Dictionary<TeamSO, int> teamPoints = new();

    // Histórico por corrida
    public Dictionary<int, Dictionary<TeamSO, int>> raceResults = new();

    // Equipes eliminadas
    public HashSet<TeamSO> eliminatedTeams = new();

    public bool IsFinal => currentRaceIndex == 3;
}
