using UnityEngine;
using UnityEditor;

[CreateAssetMenu(fileName = "New RacerProfile", menuName = "Racing/Racer Profile")]
public class RacerProfileSO : ScriptableObject
{
    public string characterID; // O ID que usaremos para comparar
    public string racerName;
}