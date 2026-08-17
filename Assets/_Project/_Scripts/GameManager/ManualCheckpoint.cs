using UnityEngine;

public class ManualCheckpoint : MonoBehaviour
{
    [SerializeField]Transform respawnPoint;
    [SerializeField] BoxCollider area;

    private Vector3 respawnPosition;
    private Quaternion respawnRotation;

    void Start()
    {
        respawnPosition = respawnPoint.position;
        respawnRotation = respawnPoint.rotation;
    }

    void OnTriggerEnter(Collider col)
    {
        SCR_RayBasedCarPhysics carPhysics = col.gameObject.GetComponent<SCR_RayBasedCarPhysics>();
        if(carPhysics != null)
        {
            carPhysics.SetRespawnPosition(respawnPosition, respawnRotation);
        }
    }
}
