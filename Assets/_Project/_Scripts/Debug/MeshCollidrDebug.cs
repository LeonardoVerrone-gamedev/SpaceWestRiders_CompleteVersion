using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(MeshCollider))]
public class SCR_MeshColliderDebugger : MonoBehaviour
{
    [Header("Configurações de Visualização")]
    [SerializeField] private bool drawWireframe = true;
    [SerializeField] private bool drawTriangles = false;
    [SerializeField] private bool drawVertices = false;
    [SerializeField] private bool drawNormals = false;
    [SerializeField] private bool drawBounds = false;
    
    [Header("Cores")]
    [SerializeField] private Color wireframeColor = Color.red;
    [SerializeField] private Color triangleColor = new Color(1f, 0.5f, 0f, 0.3f); // Laranja semi-transparente
    [SerializeField] private Color vertexColor = Color.yellow;
    [SerializeField] private Color normalColor = Color.cyan;
    [SerializeField] private Color boundsColor = Color.green;
    
    [Header("Filtros")]
    [SerializeField] private bool filterByDistance = false;
    [SerializeField] private float maxDrawDistance = 100f;
    [SerializeField] private Transform playerTransform;
    
    [Header("Performance")]
    [SerializeField] private bool useThreading = false;
    [SerializeField] private float updateInterval = 0.1f;
    
    // Cache dos dados do mesh
    private MeshCollider meshCollider;
    private Mesh cachedMesh;
    private Vector3[] vertices;
    private int[] triangles;
    private Vector3[] normals;
    
    // Dados processados para desenho otimizado
    private List<Vector3> worldVertices;
    private List<LineSegment> wireframeLines;
    private List<TriangleData> triangleList;
    
    // Controle de atualização
    private float lastUpdateTime;
    private bool isDirty = true;
    
    // Structs para dados
    private struct LineSegment
    {
        public Vector3 start;
        public Vector3 end;
        
        public LineSegment(Vector3 s, Vector3 e)
        {
            start = s;
            end = e;
        }
    }
    
    private struct TriangleData
    {
        public Vector3 v1, v2, v3;
        public Vector3 center;
        public float area;
        
        public TriangleData(Vector3 a, Vector3 b, Vector3 c)
        {
            v1 = a;
            v2 = b;
            v3 = c;
            center = (a + b + c) / 3f;
            area = Vector3.Cross(b - a, c - a).magnitude * 0.5f;
        }
    }
    
    void Awake()
    {
        meshCollider = GetComponent<MeshCollider>();
        if (meshCollider == null)
        {
            Debug.LogError("SCR_MeshColliderDebugger: MeshCollider component not found!");
            enabled = false;
            return;
        }
        
        if (meshCollider.sharedMesh == null)
        {
            Debug.LogError("SCR_MeshColliderDebugger: MeshCollider has no shared mesh assigned!");
            enabled = false;
            return;
        }
        
        CacheMeshData();
    }
    
    void Start()
    {
        if (playerTransform == null && Camera.main != null)
        {
            playerTransform = Camera.main.transform;
        }
    }
    
    void Update()
    {
        if (Time.time - lastUpdateTime >= updateInterval)
        {
            isDirty = true;
            lastUpdateTime = Time.time;
        }
    }
    
    void CacheMeshData()
    {
        cachedMesh = meshCollider.sharedMesh;
        
        // Copiar dados do mesh (importante para não modificar o original)
        vertices = cachedMesh.vertices;
        triangles = cachedMesh.triangles;
        normals = cachedMesh.normals;
        
        ProcessMeshData();
    }
    
    void ProcessMeshData()
    {
        if (vertices == null || triangles == null) return;
        
        // Converter vértices para espaço mundial
        worldVertices = new List<Vector3>();
        foreach (var vert in vertices)
        {
            worldVertices.Add(transform.TransformPoint(vert));
        }
        
        // Gerar linhas do wireframe (arestas únicas sem duplicação)
        var edgeSet = new HashSet<Edge>();
        wireframeLines = new List<LineSegment>();
        
        for (int i = 0; i < triangles.Length; i += 3)
        {
            int i1 = triangles[i];
            int i2 = triangles[i + 1];
            int i3 = triangles[i + 2];
            
            Vector3 v1 = worldVertices[i1];
            Vector3 v2 = worldVertices[i2];
            Vector3 v3 = worldVertices[i3];
            
            // Adicionar arestas sem duplicar
            AddEdge(edgeSet, v1, v2);
            AddEdge(edgeSet, v2, v3);
            AddEdge(edgeSet, v3, v1);
        }
        
        // Converter arestas para line segments
        foreach (var edge in edgeSet)
        {
            wireframeLines.Add(new LineSegment(edge.p1, edge.p2));
        }
        
        // Gerar dados dos triângulos
        triangleList = new List<TriangleData>();
        for (int i = 0; i < triangles.Length; i += 3)
        {
            triangleList.Add(new TriangleData(
                worldVertices[triangles[i]],
                worldVertices[triangles[i + 1]],
                worldVertices[triangles[i + 2]]
            ));
        }
        
        isDirty = false;
    }
    
    private void AddEdge(HashSet<Edge> edgeSet, Vector3 p1, Vector3 p2)
    {
        var edge = new Edge(p1, p2);
        if (!edgeSet.Contains(edge))
        {
            edgeSet.Add(edge);
        }
    }
    
    private class Edge
    {
        public Vector3 p1, p2;
        
        public Edge(Vector3 a, Vector3 b)
        {
            // Ordenar para comparação consistente
            if (a.GetHashCode() < b.GetHashCode())
            {
                p1 = a;
                p2 = b;
            }
            else
            {
                p1 = b;
                p2 = a;
            }
        }
        
        public override bool Equals(object obj)
        {
            if (obj is Edge other)
            {
                return p1 == other.p1 && p2 == other.p2;
            }
            return false;
        }
        
        public override int GetHashCode()
        {
            return p1.GetHashCode() ^ p2.GetHashCode();
        }
    }
    
    void OnDrawGizmos()
    {
        if (!enabled || meshCollider == null || meshCollider.sharedMesh == null)
            return;
        
        // Atualizar dados se necessário (no editor)
        #if UNITY_EDITOR
        if (!Application.isPlaying && (vertices == null || isDirty))
        {
            CacheMeshData();
        }
        #endif
        
        if (worldVertices == null) return;
        
        // Verificar distância do player
        if (filterByDistance && playerTransform != null)
        {
            float dist = Vector3.Distance(transform.position, playerTransform.position);
            if (dist > maxDrawDistance) return;
            
            // Ajustar alpha baseado na distância
            float alpha = Mathf.Clamp01(1f - (dist / maxDrawDistance));
            DrawWithAlpha(alpha);
        }
        else
        {
            DrawWithAlpha(1f);
        }
    }
    
    private void DrawWithAlpha(float alpha)
    {
        // Desenhar wireframe (linhas vermelhas)
        if (drawWireframe && wireframeLines != null)
        {
            Gizmos.color = new Color(wireframeColor.r, wireframeColor.g, wireframeColor.b, alpha);
            foreach (var line in wireframeLines)
            {
                Gizmos.DrawLine(line.start, line.end);
            }
        }
        
        // Desenhar triângulos semi-transparentes
        if (drawTriangles && triangleList != null)
        {
            Color triColor = new Color(triangleColor.r, triangleColor.g, triangleColor.b, triangleColor.a * alpha);
            Gizmos.color = triColor;
            foreach (var tri in triangleList)
            {
                DrawTriangle(tri.v1, tri.v2, tri.v3);
            }
        }
        
        // Desenhar vértices
        if (drawVertices)
        {
            Gizmos.color = new Color(vertexColor.r, vertexColor.g, vertexColor.b, alpha);
            foreach (var vert in worldVertices)
            {
                Gizmos.DrawSphere(vert, 0.05f);
            }
        }
        
        // Desenhar normais
        if (drawNormals && normals != null && vertices != null)
        {
            Gizmos.color = new Color(normalColor.r, normalColor.g, normalColor.b, alpha);
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 worldPos = transform.TransformPoint(vertices[i]);
                Vector3 worldNormal = transform.TransformDirection(normals[i]);
                Gizmos.DrawRay(worldPos, worldNormal * 0.5f);
            }
        }
        
        // Desenhar bounds
        if (drawBounds)
        {
            Gizmos.color = new Color(boundsColor.r, boundsColor.g, boundsColor.b, alpha);
            Gizmos.DrawWireCube(meshCollider.bounds.center, meshCollider.bounds.size);
        }
    }
    
    private void DrawTriangle(Vector3 v1, Vector3 v2, Vector3 v3)
    {
        Gizmos.DrawLine(v1, v2);
        Gizmos.DrawLine(v2, v3);
        Gizmos.DrawLine(v3, v1);
    }
    
    // Método para forçar recálculo dos dados
    public void RefreshMeshData()
    {
        CacheMeshData();
        isDirty = false;
    }
    
    // Método para debug em runtime via console
    public void LogMeshInfo()
    {
        if (cachedMesh == null)
        {
            Debug.LogWarning("No mesh data available!");
            return;
        }
        
        Debug.Log($"=== Mesh Collider Debug Info ===");
        Debug.Log($"GameObject: {gameObject.name}");
        Debug.Log($"Vertices: {vertices?.Length ?? 0}");
        Debug.Log($"Triangles: {(triangles?.Length ?? 0) / 3}");
        Debug.Log($"Wireframe Lines: {wireframeLines?.Count ?? 0}");
        Debug.Log($"Bounds Center: {meshCollider.bounds.center}");
        Debug.Log($"Bounds Size: {meshCollider.bounds.size}");
        
        // Calcular área total
        if (triangleList != null)
        {
            float totalArea = 0f;
            foreach (var tri in triangleList)
            {
                totalArea += tri.area;
            }
            Debug.Log($"Total Surface Area: {totalArea:F2} m²");
        }
    }
}

#if UNITY_EDITOR
// Editor customizado para controles mais fáceis
[CustomEditor(typeof(SCR_MeshColliderDebugger))]
public class SCR_MeshColliderDebuggerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        
        SCR_MeshColliderDebugger debugger = (SCR_MeshColliderDebugger)target;
        
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Debug Controls", EditorStyles.boldLabel);
        
        if (GUILayout.Button("Refresh Mesh Data"))
        {
            debugger.RefreshMeshData();
        }
        
        if (GUILayout.Button("Log Mesh Info to Console"))
        {
            debugger.LogMeshInfo();
        }
        
        EditorGUILayout.Space(5);
        EditorGUILayout.HelpBox(
            "Wireframe: Desenha todas as arestas do mesh\n" +
            "Triangles: Desenha cada triângulo individual\n" +
            "Vertices: Mostra pontos dos vértices\n" +
            "Normals: Mostra direção das normais\n" +
            "Filter by Distance: Só desenha perto do player",
            MessageType.Info
        );
    }
}
#endif