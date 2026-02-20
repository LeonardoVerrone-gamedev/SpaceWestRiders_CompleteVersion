using System.Collections.Generic;

[System.Serializable]
public class TeamPointsEntry
{
    public TeamSO team;
    public float points;
}

[System.Serializable]
public class RacePointsEntry
{
    public int raceIndex;
    public List<TeamPointsEntry> teamPoints = new();
}
