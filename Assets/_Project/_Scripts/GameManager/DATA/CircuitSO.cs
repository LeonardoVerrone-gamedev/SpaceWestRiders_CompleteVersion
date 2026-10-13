using UnityEngine;
using UnityEngine.Video;

[CreateAssetMenu(fileName = "New Circuit", menuName = "Racing/Circuit")]
public class CircuitSO : ScriptableObject
{
    public string sceneName;
    public string circuitID;
    public string circuitName;

    public Sprite circuitImage;
    public VideoClip circuitVideo;

    public int lapCount;
    public bool allowRubberBanding;

    public RacerProfileSO[] rivals;

    public float DifficultyLevel; //0 a 1
}
