using UnityEngine;
using UnityEditor;

[CreateAssetMenu(fileName = "CharacterInfo", menuName = "Racing/Character Info")]
public class SO_CharacterInfo : ScriptableObject
{
    public int characterID; // O ID que usaremos para comparar
    public string racerName;
}