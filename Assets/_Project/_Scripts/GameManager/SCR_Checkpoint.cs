using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class SCR_CheckpointTrigger : MonoBehaviour
{
    private BoxCollider myCollider;

    void Start() => myCollider = GetComponent<BoxCollider>();

    void OnTriggerEnter(Collider other)
    {
        // Tenta pegar o RacerStatus de quem entrou no trigger
        RacerStatus racer = other.GetComponentInParent<RacerStatus>();
        if (racer != null)
        {
            RaceManager.Instance.NotifyCheckpoint(racer, myCollider);
        }
    }
}