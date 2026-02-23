using UnityEngine;
using System.Collections.Generic;

public class SCR_MeshDeformer : MonoBehaviour
{
    [System.Serializable]
    public struct MeshData
    {
        public MeshFilter filter;
        [HideInInspector] public Mesh mesh;
        [HideInInspector] public Vector3[] originalVertices;
        [HideInInspector] public Vector3[] modifiedVertices;
    }

    private SCR_CarVisualCulling culling;

    [SerializeField] private List<MeshData> carParts = new List<MeshData>();

    [Header("Deformation Settings")]
    public float radius = 1.2f;
    public float deformationMultiplier = 0.02f;
    public float maxDeformation = 0.8f;

    void Start()
    {
        culling = GetComponent<SCR_CarVisualCulling>();

        for (int i = 0; i < carParts.Count; i++)
        {
            var part = carParts[i];
            if (part.filter == null) continue;

            part.mesh = part.filter.mesh;
            part.originalVertices = part.mesh.vertices;
            part.modifiedVertices = (Vector3[])part.originalVertices.Clone();

            carParts[i] = part;
        }
    }

    public void Deform(Vector3 worldPoint, Vector3 worldForce)
    {
        if (culling != null && !culling.visible) return;

        float impactStrength = worldForce.magnitude;
        if (impactStrength < 0.1f) return;

        foreach (var part in carParts)
        {
            if (part.filter == null) continue;

            Transform t = part.filter.transform;

            Vector3 localPoint = t.InverseTransformPoint(worldPoint);
            Vector3 localForce = t.InverseTransformDirection(worldForce).normalized;

            bool changed = false;

            for (int i = 0; i < part.modifiedVertices.Length; i++)
            {
                float sqrDist = (part.modifiedVertices[i] - localPoint).sqrMagnitude;
                if (sqrDist > radius * radius) continue;

                float distance = Mathf.Sqrt(sqrDist);
                float falloff = Mathf.Pow((radius - distance) / radius, 2.5f);

                Vector3 currentOffset = part.modifiedVertices[i] - part.originalVertices[i];

                if (currentOffset.magnitude < maxDeformation)
                {
                    Vector3 deformAmount = localForce * impactStrength * deformationMultiplier * falloff;
                    part.modifiedVertices[i] += deformAmount;
                    changed = true;
                }
            }

            if (changed)
            {
                part.mesh.vertices = part.modifiedVertices;
                part.mesh.RecalculateNormals();
                part.mesh.RecalculateBounds();
            }
        }
    }

    public void RestoreMesh()
    {
        foreach (var part in carParts)
        {
            if (part.mesh == null) continue;

            System.Array.Copy(part.originalVertices, part.modifiedVertices, part.originalVertices.Length);
            part.mesh.vertices = part.modifiedVertices;
            part.mesh.RecalculateNormals();
            part.mesh.RecalculateBounds();
        }
    }
}