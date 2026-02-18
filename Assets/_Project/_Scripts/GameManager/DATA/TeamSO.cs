using UnityEngine;

[CreateAssetMenu(fileName = "New Team", menuName = "Racing/Team")]
public class TeamSO : ScriptableObject
{
    public RacerProfileSO[] racers;
    public string teamName;
    public Sprite teamImage;
}
