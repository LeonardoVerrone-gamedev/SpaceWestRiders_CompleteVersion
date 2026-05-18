using UnityEngine;
using System.Collections;

public class SCR_CarCrashPhysics : MonoBehaviour
{
    private Rigidbody rb;
    private SCR_RayBasedCarPhysics carPhysics;
    private SCR_MeshDeformer deformer;
    private RacerStatus racerStatus;

    [Header("Crash Settings")]
    [SerializeField] float verticalJumpVelocity = 8f;
    [SerializeField] float rollAngularVelocity = 20f;
    [SerializeField] public float crashDuration = 3.5f;
    [SerializeField] float extraGravity = 18f;

    private Vector3 originalCenterOfMass;
    private float originalAngularDrag;

    public bool isCrashing { get; private set; }

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        carPhysics = GetComponent<SCR_RayBasedCarPhysics>();
        racerStatus = GetComponent<RacerStatus>();
        deformer = GetComponent<SCR_MeshDeformer>();

        originalCenterOfMass = rb.centerOfMass;
        originalAngularDrag = rb.angularDamping;
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

        // Desestabiliza levemente
        rb.centerOfMass = originalCenterOfMass + transform.up * 0.5f;
        rb.angularDamping = 0.5f;

        // Impulso vertical
        rb.AddForce(transform.up * verticalJumpVelocity, ForceMode.VelocityChange);

        // Torque coerente com direção do carro
        Vector3 velocityDir = rb.linearVelocity.normalized;
        Vector3 sideAxis = Vector3.Cross(velocityDir, Vector3.up);
        Vector3 spinTorque = sideAxis * Random.Range(0.8f, 1.2f) * rollAngularVelocity;

        rb.AddTorque(spinTorque, ForceMode.VelocityChange);

        float elapsed = 0f;
        float duration = racerStatus.isPlayer ? crashDuration : crashDuration * 0.75f;

        while (elapsed < duration)
        {
            elapsed += Time.fixedDeltaTime;
            float progress = elapsed / duration;

            // Gravidade extra
            rb.AddForce(Vector3.down * extraGravity, ForceMode.Acceleration);

            // Perda natural de rotação
            rb.angularDamping = Mathf.Lerp(0.5f, 4f, progress);

            // Alinhamento gradual apenas para player
            if (racerStatus.isPlayer && progress > 0.6f)
            {
                Transform target = GetLookAheadWaypoint();
                if (target != null)
                {
                    float strength = Mathf.Pow((progress - 0.6f) / 0.4f, 2f);
                    AlignTowardsWaypoint(target, strength);
                }
            }

            yield return new WaitForFixedUpdate();
        }

        // Finalização
        rb.centerOfMass = originalCenterOfMass;
        rb.angularDamping = originalAngularDrag;
        rb.angularVelocity = Vector3.zero;

        isCrashing = false;
        carPhysics.crashing = false;
    }

    private void AlignTowardsWaypoint(Transform nextPoint, float strength)
    {
        Vector3 targetDir = (nextPoint.position - transform.position).normalized;
        Quaternion targetRot = Quaternion.LookRotation(targetDir, Vector3.up);

        Quaternion delta = targetRot * Quaternion.Inverse(transform.rotation);
        delta.ToAngleAxis(out float angle, out Vector3 axis);

        if (angle > 180f) angle -= 360f;
        if (axis == Vector3.zero || float.IsNaN(axis.x)) return;

        Vector3 torque = axis * angle * strength;
        torque -= rb.angularVelocity * 0.5f; // damping

        rb.AddTorque(torque, ForceMode.Acceleration);
    }

    private Transform GetLookAheadWaypoint()
    {
        if (racerStatus == null || racerStatus.waypoints.Count == 0) return null;

        float speed = rb.linearVelocity.magnitude;
        int lookAhead = Mathf.Clamp(Mathf.RoundToInt(speed / 6f), 2, 7);
        int index = (racerStatus.currentWaypointIndex + lookAhead) % racerStatus.waypoints.Count;

        return racerStatus.waypoints[index];
    }

    void OnCollisionEnter(Collision collision)
    {
        if (!isCrashing || deformer == null) return;

        ContactPoint contact = collision.contacts[0];

        float impactForce = collision.relativeVelocity.magnitude;
        Vector3 deformForce = -contact.normal * impactForce;

        deformer.Deform(contact.point, deformForce);
    }
}