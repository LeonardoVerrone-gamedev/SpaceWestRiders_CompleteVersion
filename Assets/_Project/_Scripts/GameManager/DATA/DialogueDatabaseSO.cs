using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public struct DialogueLine
{
    public string racerID; // Quem está falando
    public string text;    // O que está falando
}

[System.Serializable]
public struct DialogueConversation
{
    public string conversationID;
    public DialogueLine challenge; // A fala inicial (ex: Ultrapassagem)
    public DialogueLine response;  // A resposta do outro piloto
}

[CreateAssetMenu(fileName = "New Dialogue Database", menuName = "Racing/Dialogue Database")]
public class DialogueDatabaseSO : ScriptableObject
{
    // Organizaremos por "Chave de Relacionamento" (ex: "Racer1_Racer2")
    public List<DialogueConversation> collisionDialogues;
    public List<DialogueConversation> overtakeDialogues;
}