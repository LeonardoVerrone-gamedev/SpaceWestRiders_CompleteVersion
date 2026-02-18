using UnityEngine;

public class SCR_MeshDeformer : MonoBehaviour
{
    [SerializeField]private MeshFilter meshFilter;
    private Mesh mesh;
    private Vector3[] originalVertices;
    private Vector3[] modifiedVertices;

    [Header("Settings")]
    public float radius = 1.0f;       // Tamanho da área afetada
    public float deformation = 0.4f; // O quanto o metal "afunda"

    void Start()
    {
        mesh = meshFilter.mesh;
        originalVertices = mesh.vertices;
        modifiedVertices = (Vector3[])originalVertices.Clone();
    }

    public void Deform(Vector3 worldContactPoint, Vector3 worldContactVelocity)
    {
        // Converte o ponto de impacto para o espaço local do carro
        Transform meshTransform = meshFilter.transform;
        
        Vector3 localPoint = meshTransform.InverseTransformPoint(worldContactPoint);
        Vector3 localVel = meshTransform.InverseTransformDirection(worldContactVelocity);

        for (int i = 0; i < modifiedVertices.Length; i++)
        {
            float dist = (modifiedVertices[i] - localPoint).magnitude;
            if (dist < radius)
            {
                // Move o vértice para dentro baseado na força do impacto
                float falloff = (radius - dist) / radius;
                modifiedVertices[i] += localVel.normalized * falloff * deformation;
            }
        }
        UpdateMesh();
    }

    public void RestoreMesh()
    {
        // Retorna aos vértices originais (pode ser chamado via Coroutine para ser suave)
        System.Array.Copy(originalVertices, modifiedVertices, originalVertices.Length);
        UpdateMesh();
    }

    private void UpdateMesh()
    {
        mesh.vertices = modifiedVertices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
    }
}