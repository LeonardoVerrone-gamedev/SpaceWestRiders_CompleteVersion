using UnityEngine;
using System.Collections;

public class SCR_CarCrashPhysics : MonoBehaviour
{
    private Rigidbody rb;
    private SCR_RayBasedCarPhysics carPhysics;
    SCR_MeshDeformer deformer;
    private RacerStatus racerStatus;

    [Header("Crash Settings")]
    [SerializeField] float verticalJumpVelocity = 10f;
    [SerializeField] float rollAngularVelocity = 25f;
    [SerializeField] public float crashDuration = 3.5f;
    
    private Vector3 originalCenterOfMass;
    public bool isCrashing { get; private set; } = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        carPhysics = GetComponent<SCR_RayBasedCarPhysics>();
        racerStatus = GetComponent<RacerStatus>();
        deformer = GetComponent<SCR_MeshDeformer>();
        originalCenterOfMass = rb.centerOfMass;
    }

    public void TriggerCrash()
    {
        if (isCrashing) return;
        StartCoroutine(CrashRoutine());
    }

    private IEnumerator CrashRoutine()
    {
        isCrashing = true;
        carPhysics.crashing = true;

        rb.centerOfMass = new Vector3(0, 1.8f, 0); 
        rb.AddForce(Vector3.up * verticalJumpVelocity, ForceMode.VelocityChange);
        
        Vector3 intenseSpin = new Vector3(
            Random.Range(0.7f, 1f) * (Random.value > 0.5f ? 1 : -1),
            Random.Range(-0.2f, 0.2f), 
            Random.Range(0.7f, 1f) * (Random.value > 0.5f ? 1 : -1)
        ).normalized * rollAngularVelocity;
        
        rb.AddTorque(intenseSpin, ForceMode.VelocityChange);

        float elapsed = 0;
        float effectiveDuration = racerStatus.isPlayer ? crashDuration : (crashDuration / 1.5f);

        while (elapsed < effectiveDuration)
        {
            elapsed += Time.fixedDeltaTime;
            float progress = elapsed / effectiveDuration;

            if (racerStatus.isPlayer)
            {
                // ATRASO: Só começamos a alinhar após 60% do tempo
                if (progress > 0.6f)
                {
                    Transform dynamicTarget = GetLookAheadWaypoint();
                    if (dynamicTarget != null)
                    {
                        if (progress > 0.85f)
                        {
                            rb.angularVelocity = Vector3.Lerp(rb.angularVelocity, Vector3.zero, Time.fixedDeltaTime * 7f);
                        }

                        float alignmentStrength = Mathf.Pow(progress, 4); 
                        AlignTowardsWaypoint(dynamicTarget, alignmentStrength);
                    }
                }
            }

            // Gravidade extra
            rb.AddForce(Vector3.down * 18f, ForceMode.Acceleration);
            yield return new WaitForFixedUpdate();
        }

        // 3. FINALIZAÇÃO
        Transform finalTarget = GetLookAheadWaypoint();
        FinalizeRotation(finalTarget);

        deformer.RestoreMesh();

        // 4. RESTAURAR ESTADO
        rb.centerOfMass = originalCenterOfMass;
        rb.angularVelocity = Vector3.zero;
        isCrashing = false;
        carPhysics.crashing = false;
    }

    private void AlignTowardsWaypoint(Transform nextPoint, float strength)
    {
        Vector3 targetDir = (nextPoint.position - transform.position).normalized;
        Quaternion targetRot = Quaternion.LookRotation(targetDir, Vector3.up);

        Quaternion deltaRot = targetRot * Quaternion.Inverse(transform.rotation);
        deltaRot.ToAngleAxis(out float angle, out Vector3 axis);

        if (angle > 180f) angle -= 360f;

        if (axis != Vector3.zero && !float.IsNaN(axis.x))
        {
            rb.AddTorque(axis * (angle * strength * 1.2f), ForceMode.Acceleration);
        }
    }

    private void FinalizeRotation(Transform nextPoint)
    {
        if (racerStatus.isPlayer && nextPoint != null)
        {
            Vector3 finalDir = (nextPoint.position - transform.position).normalized;
            transform.rotation = Quaternion.LookRotation(finalDir, Vector3.up);
        }
    }

    private Transform GetLookAheadWaypoint()
    {
        if (racerStatus == null || racerStatus.waypoints.Count == 0) return null;
        float speed = rb.linearVelocity.magnitude;
        int lookAheadAmount = Mathf.Clamp(Mathf.RoundToInt(speed / 6f), 7, 15);
        int targetIndex = (racerStatus.currentWaypointIndex + lookAheadAmount) % racerStatus.waypoints.Count;
        return racerStatus.waypoints[targetIndex];
    }

    void OnCollisionEnter(Collision collision)
    {
        if (isCrashing)
        {
            deformer.Deform(collision.contacts[0].point, collision.relativeVelocity);
        }
    }
}