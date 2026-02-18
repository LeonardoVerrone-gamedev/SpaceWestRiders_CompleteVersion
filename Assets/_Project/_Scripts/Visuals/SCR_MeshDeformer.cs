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
    SCR_CarVisualCulling culling;

    [SerializeField] private List<MeshData> carParts = new List<MeshData>();

    [Header("Settings")]
    public float radius = 1.2f;       
    public float deformation = 0.5f; 

    void Start()
    {
        culling = GetComponent<SCR_CarVisualCulling>();

        // Inicializa todas as peças da lista
        for (int i = 0; i < carParts.Count; i++)
        {
            var part = carParts[i];
            if (part.filter == null) continue;

            part.mesh = part.filter.mesh; // Instância única
            part.originalVertices = part.mesh.vertices;
            part.modifiedVertices = (Vector3[])part.originalVertices.Clone();
            
            carParts[i] = part; // Salva de volta na lista
        }
    }

    public void Deform(Vector3 worldContactPoint, Vector3 worldContactVelocity)
    {
        if(!culling.visible)return;
        // Percorre cada peça do carro (capô, portas, etc)
        foreach (var part in carParts)
        {
            if (part.filter == null) continue;

            Transform partTransform = part.filter.transform;
            
            // Converte o impacto global para o espaço local DESTA peça específica
            Vector3 localPoint = partTransform.InverseTransformPoint(worldContactPoint);
            Vector3 localVel = partTransform.InverseTransformDirection(worldContactVelocity);
            Vector3 deformDir = localVel.normalized;

            bool meshChanged = false;

            for (int i = 0; i < part.modifiedVertices.Length; i++)
            {
                float dist = (part.modifiedVertices[i] - localPoint).sqrMagnitude;
                
                // Usamos sqrMagnitude por performance (evita raiz quadrada)
                if (dist < radius * radius)
                {
                    float distance = Mathf.Sqrt(dist);
                    float falloff = (radius - distance) / radius;
                    
                    part.modifiedVertices[i] += deformDir * falloff * deformation;
                    meshChanged = true;
                }
            }

            if (meshChanged)
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