using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;
using System.Collections.Generic;
using static Unity.Mathematics.math;

[RequireComponent(typeof(SplineContainer))]
public class RaceTrackGenerator : MonoBehaviour
{
    [Header("Track Settings")]
    [SerializeField] private float trackWidth = 10f;
    [SerializeField] private float trackHeight = 0.5f;
    [SerializeField] private int resolution = 100;
    [SerializeField] private bool generateCollider = true;

    [Header("Materials")]
    [SerializeField] private Material trackMaterial;
    [SerializeField] private Texture2D trackTexture;
    [SerializeField] private Material wallMaterial;
    [SerializeField] private Texture2D wallTexture;

    [Header("Side Walls")]
    [SerializeField] private bool generateWalls = true;
    [SerializeField] private float wallHeight = 3f;
    [SerializeField] private float wallThickness = 0.5f;

    private SplineContainer splineContainer;
    private GameObject trackMeshObject;
    private GameObject wallsObject;

    private void OnValidate()
    {
        if (trackTexture != null && trackMaterial != null)
        {
            trackMaterial.mainTexture = trackTexture;
        }
        if (wallTexture != null && wallMaterial != null)
        {
            wallMaterial.mainTexture = wallTexture;
        }
    }

    [ContextMenu("Generate Track")]
    public void GenerateTrack()
    {
        splineContainer = GetComponent<SplineContainer>();
        
        if (splineContainer == null || splineContainer.Spline == null)
        {
            Debug.LogError("No SplineContainer or Spline found!");
            return;
        }

        Cleanup();
        GenerateTrackMesh();
        
        if (generateWalls)
        {
            GenerateSideWalls();
        }
        
        Debug.Log("Track generation complete!");
    }

    [ContextMenu("Cleanup")]
    public void Cleanup()
    {
        if (trackMeshObject != null) DestroyImmediate(trackMeshObject);
        if (wallsObject != null) DestroyImmediate(wallsObject);
    }

    private void GenerateTrackMesh()
    {
        trackMeshObject = new GameObject("TrackMesh");
        trackMeshObject.transform.SetParent(transform);
        trackMeshObject.transform.localPosition = Vector3.zero;
        trackMeshObject.transform.localRotation = Quaternion.identity;
        trackMeshObject.transform.localScale = Vector3.one;

        MeshFilter meshFilter = trackMeshObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = trackMeshObject.AddComponent<MeshRenderer>();
        meshRenderer.material = trackMaterial;

        Mesh mesh = new Mesh();
        mesh.name = "TrackMesh";

        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector2> uv = new List<Vector2>();
        List<int> triangles = new List<int>();

        Spline spline = splineContainer.Spline;
        float totalLength = spline.GetLength();

        List<Vector3> leftPoints = new List<Vector3>();
        List<Vector3> rightPoints = new List<Vector3>();
        List<Vector3> binormals = new List<Vector3>();

        for (int i = 0; i <= resolution; i++)
        {
            float t = i / (float)resolution;
            spline.Evaluate(t, out float3 positionFloat3, out float3 tangentFloat3, out float3 upVectorFloat3);
            
            Vector3 position = positionFloat3;
            Vector3 tangent = normalize(tangentFloat3);
            
            Vector3 normal = Vector3.up;
            Vector3 binormal = Vector3.Cross(tangent, normal).normalized;
            binormals.Add(binormal);

            Vector3 leftPos = position - binormal * (trackWidth * 0.5f);
            leftPos.y += trackHeight;
            leftPoints.Add(leftPos);

            Vector3 rightPos = position + binormal * (trackWidth * 0.5f);
            rightPos.y += trackHeight;
            rightPoints.Add(rightPos);
        }

        // Criar vértices SUPERIORES (topo da pista)
        for (int i = 0; i <= resolution; i++)
        {
            float t = i / (float)resolution;
            float trackLengthU = (totalLength * t) / 10f;
            float widthV = trackWidth / 10f;

            vertices.Add(leftPoints[i]);
            vertices.Add(rightPoints[i]);
            
            uv.Add(new Vector2(trackLengthU, 0));
            uv.Add(new Vector2(trackLengthU, widthV));
            
            normals.Add(Vector3.up);
            normals.Add(Vector3.up);
        }

        // Criar triângulos para o TOPO da pista
        for (int i = 0; i < resolution; i++)
        {
            int current = i * 2;
            int next = (i + 1) * 2;

            triangles.Add(current);
            triangles.Add(current + 1);
            triangles.Add(next);

            triangles.Add(current + 1);
            triangles.Add(next + 1);
            triangles.Add(next);
        }

        // Adicionar LATERAIS da pista
        int sideVerticesStart = vertices.Count;
        
        // Lado esquerdo
        for (int i = 0; i <= resolution; i++)
        {
            // Topo
            vertices.Add(leftPoints[i]);
            uv.Add(new Vector2(i / (float)resolution, 1));
            normals.Add(-binormals[i]);
            
            // Base
            Vector3 basePos = leftPoints[i];
            basePos.y = 0;
            vertices.Add(basePos);
            uv.Add(new Vector2(i / (float)resolution, 0));
            normals.Add(-binormals[i]);
        }
        
        // Triângulos para lado esquerdo
        for (int i = 0; i < resolution; i++)
        {
            int current = sideVerticesStart + (i * 2);
            int next = sideVerticesStart + ((i + 1) * 2);
            
            triangles.Add(current);
            triangles.Add(next);
            triangles.Add(current + 1);
            
            triangles.Add(current + 1);
            triangles.Add(next);
            triangles.Add(next + 1);
        }
        
        // Lado direito
        int rightSideStart = vertices.Count;
        for (int i = 0; i <= resolution; i++)
        {
            // Topo
            vertices.Add(rightPoints[i]);
            uv.Add(new Vector2(i / (float)resolution, 1));
            normals.Add(binormals[i]);
            
            // Base
            Vector3 basePos = rightPoints[i];
            basePos.y = 0;
            vertices.Add(basePos);
            uv.Add(new Vector2(i / (float)resolution, 0));
            normals.Add(binormals[i]);
        }
        
        // Triângulos para lado direito
        for (int i = 0; i < resolution; i++)
        {
            int current = rightSideStart + (i * 2);
            int next = rightSideStart + ((i + 1) * 2);
            
            triangles.Add(current);
            triangles.Add(current + 1);
            triangles.Add(next);
            
            triangles.Add(current + 1);
            triangles.Add(next + 1);
            triangles.Add(next);
        }

        mesh.vertices = vertices.ToArray();
        mesh.normals = normals.ToArray();
        mesh.uv = uv.ToArray();
        mesh.triangles = triangles.ToArray();
        
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.Optimize();

        meshFilter.mesh = mesh;

        if (generateCollider)
        {
            MeshCollider existingCollider = trackMeshObject.GetComponent<MeshCollider>();
            if (existingCollider != null)
            {
                DestroyImmediate(existingCollider);
            }
            
            MeshCollider collider = trackMeshObject.AddComponent<MeshCollider>();
            collider.sharedMesh = null;
            collider.sharedMesh = mesh;
            collider.convex = false;
        }
    }

    private void GenerateSideWalls()
    {
        wallsObject = new GameObject("SideWalls");
        wallsObject.transform.SetParent(transform);
        wallsObject.transform.localPosition = Vector3.zero;
        wallsObject.transform.localRotation = Quaternion.identity;
        wallsObject.transform.localScale = Vector3.one;

        MeshFilter meshFilter = wallsObject.AddComponent<MeshFilter>();
        MeshRenderer meshRenderer = wallsObject.AddComponent<MeshRenderer>();
        meshRenderer.material = wallMaterial;

        Mesh mesh = new Mesh();
        mesh.name = "SideWalls";

        List<Vector3> vertices = new List<Vector3>();
        List<Vector3> normals = new List<Vector3>();
        List<Vector2> uv = new List<Vector2>();
        List<int> triangles = new List<int>();

        Spline spline = splineContainer.Spline;
        float totalLength = spline.GetLength();

        List<Vector3> leftWallBottom = new List<Vector3>();
        List<Vector3> leftWallTop = new List<Vector3>();
        List<Vector3> rightWallBottom = new List<Vector3>();
        List<Vector3> rightWallTop = new List<Vector3>();
        List<Vector3> wallBinormals = new List<Vector3>();

        for (int i = 0; i <= resolution; i++)
        {
            float t = i / (float)resolution;
            spline.Evaluate(t, out float3 positionFloat3, out float3 tangentFloat3, out float3 upVectorFloat3);
            
            Vector3 position = positionFloat3;
            Vector3 tangent = normalize(tangentFloat3);
            
            Vector3 normal = Vector3.up;
            Vector3 binormal = Vector3.Cross(tangent, normal).normalized;
            wallBinormals.Add(binormal);

            // CORREÇÃO AQUI: A parede deve começar exatamente na borda da pista
            // Sem offset extra, apenas a espessura da parede
            Vector3 leftWallPos = position - binormal * (trackWidth * 0.5f);
            leftWallPos.y += trackHeight; // Mesma altura da pista
            
            Vector3 rightWallPos = position + binormal * (trackWidth * 0.5f);
            rightWallPos.y += trackHeight; // Mesma altura da pista
            
            leftWallBottom.Add(leftWallPos);
            leftWallTop.Add(leftWallPos + Vector3.up * wallHeight);
            
            rightWallBottom.Add(rightWallPos);
            rightWallTop.Add(rightWallPos + Vector3.up * wallHeight);
        }

        // Gerar mesh para parede esquerda
        // Lado interno (voltado para a pista)
        int leftWallStart = vertices.Count;
        for (int i = 0; i <= resolution; i++)
        {
            float lengthU = (totalLength * (i / (float)resolution)) / 5f;
            float heightV = wallHeight / 5f;

            // Fundo - lado interno (apontando para direita/para fora da pista)
            Vector3 innerBottom = leftWallBottom[i];
            vertices.Add(innerBottom);
            uv.Add(new Vector2(lengthU, 0));
            normals.Add(wallBinormals[i]); // Aponta para fora da pista
            
            // Topo - lado interno
            Vector3 innerTop = leftWallTop[i];
            vertices.Add(innerTop);
            uv.Add(new Vector2(lengthU, heightV));
            normals.Add(wallBinormals[i]); // Aponta para fora da pista
        }
        
        for (int i = 0; i < resolution; i++)
        {
            int current = leftWallStart + (i * 2);
            int next = leftWallStart + ((i + 1) * 2);
            
            // Triângulos para parede esquerda (lado interno)
            triangles.Add(current);
            triangles.Add(next + 1);
            triangles.Add(current + 1);
            
            triangles.Add(current);
            triangles.Add(next);
            triangles.Add(next + 1);
        }
        
        // Lado externo da parede esquerda
        int leftWallOuterStart = vertices.Count;
        for (int i = 0; i <= resolution; i++)
        {
            float lengthU = (totalLength * (i / (float)resolution)) / 5f;
            float heightV = wallHeight / 5f;

            // Fundo - lado externo (deslocado pela espessura)
            Vector3 outerBottom = leftWallBottom[i] - wallBinormals[i] * wallThickness;
            vertices.Add(outerBottom);
            uv.Add(new Vector2(lengthU, 0));
            normals.Add(-wallBinormals[i]); // Aponta para fora (oposto ao lado interno)
            
            // Topo - lado externo
            Vector3 outerTop = leftWallTop[i] - wallBinormals[i] * wallThickness;
            vertices.Add(outerTop);
            uv.Add(new Vector2(lengthU, heightV));
            normals.Add(-wallBinormals[i]); // Aponta para fora (oposto ao lado interno)
        }
        
        for (int i = 0; i < resolution; i++)
        {
            int current = leftWallOuterStart + (i * 2);
            int next = leftWallOuterStart + ((i + 1) * 2);
            
            // Triângulos para parede esquerda (lado externo)
            triangles.Add(current);
            triangles.Add(current + 1);
            triangles.Add(next + 1);
            
            triangles.Add(current);
            triangles.Add(next + 1);
            triangles.Add(next);
        }

        // Gerar mesh para parede direita
        // Lado interno (voltado para a pista)
        int rightWallStart = vertices.Count;
        for (int i = 0; i <= resolution; i++)
        {
            float lengthU = (totalLength * (i / (float)resolution)) / 5f;
            float heightV = wallHeight / 5f;

            // Fundo - lado interno (apontando para esquerda/para fora da pista)
            Vector3 innerBottom = rightWallBottom[i];
            vertices.Add(innerBottom);
            uv.Add(new Vector2(lengthU, 0));
            normals.Add(-wallBinormals[i]); // Aponta para fora da pista
            
            // Topo - lado interno
            Vector3 innerTop = rightWallTop[i];
            vertices.Add(innerTop);
            uv.Add(new Vector2(lengthU, heightV));
            normals.Add(-wallBinormals[i]); // Aponta para fora da pista
        }
        
        for (int i = 0; i < resolution; i++)
        {
            int current = rightWallStart + (i * 2);
            int next = rightWallStart + ((i + 1) * 2);
            
            // Triângulos para parede direita (lado interno)
            triangles.Add(current);
            triangles.Add(current + 1);
            triangles.Add(next + 1);
            
            triangles.Add(current);
            triangles.Add(next + 1);
            triangles.Add(next);
        }
        
        // Lado externo da parede direita
        int rightWallOuterStart = vertices.Count;
        for (int i = 0; i <= resolution; i++)
        {
            float lengthU = (totalLength * (i / (float)resolution)) / 5f;
            float heightV = wallHeight / 5f;

            // Fundo - lado externo (deslocado pela espessura)
            Vector3 outerBottom = rightWallBottom[i] + wallBinormals[i] * wallThickness;
            vertices.Add(outerBottom);
            uv.Add(new Vector2(lengthU, 0));
            normals.Add(wallBinormals[i]); // Aponta para fora (oposto ao lado interno)
            
            // Topo - lado externo
            Vector3 outerTop = rightWallTop[i] + wallBinormals[i] * wallThickness;
            vertices.Add(outerTop);
            uv.Add(new Vector2(lengthU, heightV));
            normals.Add(wallBinormals[i]); // Aponta para fora (oposto ao lado interno)
        }
        
        for (int i = 0; i < resolution; i++)
        {
            int current = rightWallOuterStart + (i * 2);
            int next = rightWallOuterStart + ((i + 1) * 2);
            
            // Triângulos para parede direita (lado externo)
            triangles.Add(current);
            triangles.Add(next + 1);
            triangles.Add(current + 1);
            
            triangles.Add(current);
            triangles.Add(next);
            triangles.Add(next + 1);
        }

        // Adicionar topo das paredes
        int topStart = vertices.Count;
        for (int i = 0; i <= resolution; i++)
        {
            float lengthU = (totalLength * (i / (float)resolution)) / 5f;
            float thicknessV = wallThickness / 5f;

            // Topo da parede esquerda (lado interno para externo)
            Vector3 leftInnerTop = leftWallTop[i];
            Vector3 leftOuterTop = leftWallTop[i] - wallBinormals[i] * wallThickness;
            
            vertices.Add(leftInnerTop);
            uv.Add(new Vector2(lengthU, 0));
            normals.Add(Vector3.up);
            
            vertices.Add(leftOuterTop);
            uv.Add(new Vector2(lengthU, thicknessV));
            normals.Add(Vector3.up);
            
            // Topo da parede direita (lado interno para externo)
            Vector3 rightInnerTop = rightWallTop[i];
            Vector3 rightOuterTop = rightWallTop[i] + wallBinormals[i] * wallThickness;
            
            vertices.Add(rightInnerTop);
            uv.Add(new Vector2(lengthU, 0));
            normals.Add(Vector3.up);
            
            vertices.Add(rightOuterTop);
            uv.Add(new Vector2(lengthU, thicknessV));
            normals.Add(Vector3.up);
        }
        
        // Triângulos para o topo das paredes
        for (int i = 0; i < resolution; i++)
        {
            // Parede esquerda
            int currentLeft = topStart + (i * 4);
            int nextLeft = topStart + ((i + 1) * 4);
            
            triangles.Add(currentLeft);
            triangles.Add(nextLeft + 1);
            triangles.Add(currentLeft + 1);
            triangles.Add(currentLeft);
            triangles.Add(nextLeft);
            triangles.Add(nextLeft + 1);
            
            // Parede direita
            int currentRight = topStart + (i * 4) + 2;
            int nextRight = topStart + ((i + 1) * 4) + 2;
            
            triangles.Add(currentRight);
            triangles.Add(currentRight + 1);
            triangles.Add(nextRight + 1);
            triangles.Add(currentRight);
            triangles.Add(nextRight + 1);
            triangles.Add(nextRight);
        }

        mesh.vertices = vertices.ToArray();
        mesh.normals = normals.ToArray();
        mesh.uv = uv.ToArray();
        mesh.triangles = triangles.ToArray();
        
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.Optimize();

        meshFilter.mesh = mesh;

        if (generateCollider)
        {
            MeshCollider collider = wallsObject.AddComponent<MeshCollider>();
            collider.sharedMesh = null;
            collider.sharedMesh = mesh;
            collider.convex = false;
        }
    }
}