using UnityEngine;
using System.Linq;

public class RaceDramaDirector : MonoBehaviour
{
    public static RaceDramaDirector Instance;

    [SerializeField] private DialogueDatabaseSO database;
    [SerializeField] private float dialogueCooldown = 5f;
    private float lastDialogueTime;

    void Awake() => Instance = this;

    // decide o contexto
    public void RequestBattleDialogue(RacerStatus attackerStatus, RacerStatus victimStatus, bool isCollision, RacerProfileSO attacker, RacerProfileSO victim)
    {
        if (!attackerStatus.isPlayer && !victimStatus.isPlayer) return;
        if (Time.time < lastDialogueTime + dialogueCooldown) return;

        var pool = isCollision ? database.collisionDialogues : database.overtakeDialogues;
        
        var possibleConversations = pool.Where(c => 
            c.challenge.racerID == attacker.racerName && 
            c.response.racerID == victim.racerName
        ).ToList();

        if (possibleConversations.Count > 0)
        {
            var selected = possibleConversations[Random.Range(0, possibleConversations.Count)];
            
            // Agora passamos os STATUS para a execução saber qual HUD ativar
            ExecuteDialogue(selected, attackerStatus, victimStatus);
            lastDialogueTime = Time.time;
        }
    }

    private void ExecuteDialogue(DialogueConversation conv, RacerStatus attackerStatus, RacerStatus victimStatus)
    {
        PlayerGameplayManager attackerHUD = attackerStatus.GetGameplayManager();
        PlayerGameplayManager victimHUD = victimStatus.GetGameplayManager();

        if (attackerStatus.isPlayer && attackerHUD != null)
        {
            if (!attackerHUD.IsBusy())
            {
                StartCoroutine(RunSequenceOnHUD(attackerHUD, conv));
            }
        }
        
        if (victimStatus.isPlayer && victimHUD != null && victimHUD != attackerHUD)
        {
            if (!victimHUD.IsBusy())
            {
                StartCoroutine(RunSequenceOnHUD(victimHUD, conv));
            }
        }
    }

    private System.Collections.IEnumerator RunSequenceOnHUD(PlayerGameplayManager hud, DialogueConversation conv)
    {
        // Fala 1: Desafio
        hud.ShowDialogue(conv.challenge.racerID, conv.challenge.text);
        
        // Espera o tempo de leitura + resposta
        yield return new WaitForSeconds(3.5f);
        
        // Fala 2: Resposta
        hud.ShowDialogue(conv.response.racerID, conv.response.text);
        
        yield return new WaitForSeconds(3f);
        hud.HideDialogue();
    }
}