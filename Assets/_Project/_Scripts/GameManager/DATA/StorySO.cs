using UnityEngine;

[CreateAssetMenu(fileName = "New Story", menuName = "Racing/Story")]
public class StorySO : ScriptableObject
{
    public string storyName;
    public string storyID;
    public CompetitionSO[] tournaments;
}
