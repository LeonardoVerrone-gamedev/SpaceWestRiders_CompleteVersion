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

    [Header("Setup Automático")]
    [Tooltip("Arraste aqui o objeto Pai que contém todas as malhas (filhos e netos). Se deixar vazio, usará este próprio objeto.")]
    [SerializeField] private Transform meshParent;

    [SerializeField] private List<MeshData> carParts = new List<MeshData>();

    SCR_RayBasedCarPhysics carPhysics;
    DamageCar damageCar;

    [Header("Deformation Settings")]
    public float radius = 1.2f;
    public float deformationMultiplier = 0.02f;
    public float maxDeformation = 0.8f;

    void Start()
    {
        carPhysics = GetComponent<SCR_RayBasedCarPhysics>();
        damageCar = GetComponent<DamageCar>();

        // Se não arrastou nada no Inspector, assume o próprio GameObject do script como raiz
        if (meshParent == null)
        {
            meshParent = transform;
        }

        // Limpa a lista por precaução antes de popular recursivamente
        carParts.Clear();

        // Varre recursivamente a partir do objeto pai e inicializa os vértices
        CollectMeshesRecursive(meshParent);
    }

    // Método recursivo que pega MeshFilters em todos os níveis de hierarquia abaixo do pai
    private void CollectMeshesRecursive(Transform current)
    {
        if (current == null) return;

        // Tenta pegar o MeshFilter no objeto atual
        MeshFilter meshFilter = current.GetComponent<MeshFilter>();
        if (meshFilter != null)
        {
            MeshData newPart = new MeshData();
            newPart.filter = meshFilter;
            
            // Instancia/pega a mesh e inicializa os dados de vértices
            newPart.mesh = meshFilter.mesh;
            newPart.originalVertices = newPart.mesh.vertices;
            newPart.modifiedVertices = (Vector3[])newPart.originalVertices.Clone();

            carParts.Add(newPart);
        }

        // Percorre todos os filhos diretos e chama a função para cada um (recursão)
        foreach (Transform child in current)
        {
            CollectMeshesRecursive(child);
        }
    }

    public void Deform(Vector3 worldPoint, Vector3 worldForce)
    {
        if(damageCar.publicCurrentDamage > 1f) return;

        float impactStrength = worldForce.magnitude;
        if (impactStrength < 0.1f) return;

        foreach (var part in carParts)
        {
            if (part.filter == null) continue;

            Transform t = part.filter.transform;

            // Ponto de impacto convertido para o espaço local da peça
            Vector3 localPoint = t.InverseTransformPoint(worldPoint);
            
            bool changed = false;

            for (int i = 0; i < part.modifiedVertices.Length; i++)
            {
                float sqrDist = (part.modifiedVertices[i] - localPoint).sqrMagnitude;
                if (sqrDist > radius * radius) continue;

                float distance = Mathf.Sqrt(sqrDist);
                
                // Evita divisão por zero caso o ponto seja idêntico ao vértice
                if (distance < 0.001f) continue; 

                float falloff = Mathf.Pow((radius - distance) / radius, 2.5f);

                Vector3 vertexToImpactDirection = (part.modifiedVertices[i] - localPoint).normalized;

                // Multiplicamos pela força do impacto e direção calculada (com sinal invertido para afundar)
                Vector3 deformAmount = -vertexToImpactDirection * impactStrength * deformationMultiplier * falloff;

                // Calcula qual seria a nova posição e o novo deslocamento totalizado
                Vector3 potentialNewVertex = part.modifiedVertices[i] + deformAmount;
                Vector3 totalOffset = potentialNewVertex - part.originalVertices[i];
                
                // TRAVA TOTAL: Limita rigidamente o estrago por vértice para durar as 3 voltas
                if (totalOffset.magnitude > maxDeformation)
                {
                    totalOffset = Vector3.ClampMagnitude(totalOffset, maxDeformation);
                    part.modifiedVertices[i] = part.originalVertices[i] + totalOffset;
                }
                else
                {
                    part.modifiedVertices[i] = potentialNewVertex;
                }

                changed = true;
            }

            if (changed)
            {
                part.mesh.vertices = part.modifiedVertices;
                //part.mesh.RecalculateNormals();
                //part.mesh.RecalculateBounds();
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
            //part.mesh.RecalculateNormals();
            //part.mesh.RecalculateBounds();
        }
    }
}