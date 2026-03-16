using UnityEngine;
using System.Collections.Generic;

public class RacerInteractionTrigger : MonoBehaviour
{
    private RacerStatus myStatus;
    private RacerProfileSO profile;
    
    // Guardamos a posição do frame anterior
    private int lastGridPosition;

    void Start()
    {
        myStatus = GetComponent<RacerStatus>();
        profile = GetComponent<SCR_CarIdentity>().racerData;
        
        // Inicializa com a posição atual
        lastGridPosition = myStatus.gridPosition;
    }

    void Update()
    {
        int currentPos = myStatus.gridPosition;

        // Se a posição mudou
        if (currentPos != lastGridPosition)
        {
            HandlePositionChange(currentPos, lastGridPosition);
            lastGridPosition = currentPos;
        }
    }

    private void HandlePositionChange(int current, int last)
    {
        // Se não é player e ninguém envolvido é player, ignore
        
        // QUEM foi o coadjuvante dessa troca de posição
        RacerStatus partner = FindPositionPartner(current, last);
        if (partner == null) return;

        var partnerProfile = partner.GetComponent<SCR_CarIdentity>().racerData;

        if (current < last) 
        {
            // EU SUBI (Ultrapassei alguém): Eu = Attacker, Partner = Victim
            RaceDramaDirector.Instance.RequestBattleDialogue(myStatus, partner, false, profile, partnerProfile);
        }
        else 
        {
            // EU DESCI (Fui ultrapassado): Partner = Attacker, Eu = Victim
            RaceDramaDirector.Instance.RequestBattleDialogue(partner, myStatus, false, partnerProfile, profile);
        }
    }

    // Busca qual carro está na posição que eu acabei de ocupar ou deixar
    private RacerStatus FindPositionPartner(int currentPos, int lastPos)
    {
        var allRacers = Object.FindObjectsByType<RacerStatus>(FindObjectsSortMode.None);
        
        foreach (var racer in allRacers)
        {
            if (racer == myStatus) continue;

            if (racer.gridPosition == lastPos)
            {
                return racer;
            }
        }
        return null;
    }

    void OnCollisionEnter(Collision collision)
    {
        RacerStatus otherStatus = collision.gameObject.GetComponentInParent<RacerStatus>();
        if (otherStatus == null || otherStatus == myStatus) return;

        var otherProfile = otherStatus.GetComponent<SCR_CarIdentity>().racerData;
        Vector3 localHitPoint = transform.InverseTransformPoint(collision.contacts[0].point);

        if (localHitPoint.z < 0) 
        {
            RaceDramaDirector.Instance.RequestBattleDialogue(otherStatus, myStatus, true, otherProfile, profile);
        }
        else 
        {
            RaceDramaDirector.Instance.RequestBattleDialogue(myStatus, otherStatus, true, profile, otherProfile);
        }
    }
}