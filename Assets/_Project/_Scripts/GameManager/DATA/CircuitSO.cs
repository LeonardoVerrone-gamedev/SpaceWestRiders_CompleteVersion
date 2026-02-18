using UnityEngine;

[CreateAssetMenu(fileName = "New Circuit", menuName = "Racing/Circuit")]
public class CircuitSO : ScriptableObject
{
    public string sceneName;
    public string circuitID;
    string circuitName;
    Sprite circuitImage;
    public int lapCount;
}
