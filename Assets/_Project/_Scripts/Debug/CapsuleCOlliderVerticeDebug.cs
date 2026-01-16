using UnityEngine;
using System.Collections.Generic;

public class CapsuleColliderDebug : MonoBehaviour
{
    [Header("Debug Settings")]
    public bool enableDebug = true;
    public float pointSize = 0.1f;
    
    [Header("Box Colliders to Check")]
    public BoxCollider[] boxCollidersToCheck;
    
    private List<CapsuleCollider> capsuleColliders = new List<CapsuleCollider>();
    private List<Vector3> verticesInsideCapsules = new List<Vector3>();

    void OnDrawGizmos()
    {
        if (!enableDebug) return;
        
        FindAllCapsuleColliders();
        FindVerticesInsideCapsules();
        DrawDebugPoints();
    }

    void FindAllCapsuleColliders()
    {
        capsuleColliders.Clear();
        
        // Busca recursiva por todos os CapsuleColliders nos children
        CapsuleCollider[] colliders = GetComponentsInChildren<CapsuleCollider>();
        capsuleColliders.AddRange(colliders);
        
        // Inclui também CapsuleColliders no próprio objeto
        CapsuleCollider selfCollider = GetComponent<CapsuleCollider>();
        if (selfCollider != null && !capsuleColliders.Contains(selfCollider))
        {
            capsuleColliders.Add(selfCollider);
        }
    }

    void FindVerticesInsideCapsules()
    {
        verticesInsideCapsules.Clear();
        
        // Verifica se há BoxColliders configurados
        if (boxCollidersToCheck == null || boxCollidersToCheck.Length == 0)
        {
            Debug.LogWarning("Nenhum BoxCollider configurado para verificação.");
            return;
        }
        
        foreach (BoxCollider boxCollider in boxCollidersToCheck)
        {
            if (boxCollider == null) continue;
            
            // Obtém os vértices do BoxCollider
            Vector3[] vertices = GetBoxColliderVertices(boxCollider);
            
            // Verifica cada vértice contra todas as cápsulas
            foreach (Vector3 vertex in vertices)
            {
                foreach (CapsuleCollider capsule in capsuleColliders)
                {
                    if (capsule == null) continue;
                    
                    if (IsPointInCapsule(vertex, capsule))
                    {
                        if (!verticesInsideCapsules.Contains(vertex))
                        {
                            verticesInsideCapsules.Add(vertex);
                        }
                        break; // Não precisa verificar outras cápsulas para este vértice
                    }
                }
            }
        }
    }

    bool IsPointInCapsule(Vector3 worldPoint, CapsuleCollider capsule)
    {
        // Converte o ponto para o espaço local da cápsula
        Vector3 localPoint = capsule.transform.InverseTransformPoint(worldPoint);
        
        // Obtém os parâmetros da cápsula
        float height = capsule.height;
        float radius = capsule.radius;
        Vector3 center = capsule.center;
        
        // Considera o scale
        Vector3 scale = capsule.transform.lossyScale;
        float scaledRadius = radius * Mathf.Max(scale.x, scale.z);
        float scaledHeight = height * scale.y;
        
        // Calcula os pontos da linha central da cápsula baseado na direção
        Vector3 topPoint, bottomPoint;
        GetCapsuleEndPoints(capsule, center, scaledHeight, scaledRadius, out topPoint, out bottomPoint);
        
        // Calcula a distância do ponto até a linha da cápsula
        float distanceToLine = DistancePointToLineSegment(localPoint, bottomPoint, topPoint);
        
        // Verifica se o ponto está dentro da cápsula (raio + margem pequena)
        return distanceToLine <= scaledRadius;
    }

    void GetCapsuleEndPoints(CapsuleCollider capsule, Vector3 center, float height, float radius, out Vector3 topPoint, out Vector3 bottomPoint)
    {
        float cylinderHeight = height - 2 * radius;
        cylinderHeight = Mathf.Max(cylinderHeight, 0);
        
        Vector3 direction = GetCapsuleDirection(capsule.direction);
        
        topPoint = center + direction * (cylinderHeight * 0.5f);
        bottomPoint = center - direction * (cylinderHeight * 0.5f);
    }

    Vector3 GetCapsuleDirection(int direction)
    {
        switch (direction)
        {
            case 0: // X-Axis
                return Vector3.right;
            case 1: // Y-Axis
                return Vector3.up;
            case 2: // Z-Axis
                return Vector3.forward;
            default:
                return Vector3.up;
        }
    }

    float DistancePointToLineSegment(Vector3 point, Vector3 lineStart, Vector3 lineEnd)
    {
        Vector3 lineVector = lineEnd - lineStart;
        float lineLength = lineVector.magnitude;
        
        if (lineLength < 0.0001f)
            return Vector3.Distance(point, lineStart);
        
        Vector3 normalizedLine = lineVector / lineLength;
        Vector3 pointToStart = point - lineStart;
        
        // Projeção do ponto na linha
        float projection = Vector3.Dot(pointToStart, normalizedLine);
        
        if (projection < 0)
            return Vector3.Distance(point, lineStart);
        else if (projection > lineLength)
            return Vector3.Distance(point, lineEnd);
        else
        {
            Vector3 closestPoint = lineStart + normalizedLine * projection;
            return Vector3.Distance(point, closestPoint);
        }
    }

    Vector3[] GetBoxColliderVertices(BoxCollider boxCollider)
    {
        Vector3[] vertices = new Vector3[8];
        
        Vector3 center = boxCollider.center;
        Vector3 size = boxCollider.size * 0.5f;
        
        // Calcula todos os 8 vértices do box collider no espaço local
        vertices[0] = center + new Vector3(-size.x, -size.y, -size.z);
        vertices[1] = center + new Vector3(-size.x, -size.y, size.z);
        vertices[2] = center + new Vector3(-size.x, size.y, -size.z);
        vertices[3] = center + new Vector3(-size.x, size.y, size.z);
        vertices[4] = center + new Vector3(size.x, -size.y, -size.z);
        vertices[5] = center + new Vector3(size.x, -size.y, size.z);
        vertices[6] = center + new Vector3(size.x, size.y, -size.z);
        vertices[7] = center + new Vector3(size.x, size.y, size.z);
        
        // Converte para o espaço mundial
        for (int i = 0; i < vertices.Length; i++)
        {
            vertices[i] = boxCollider.transform.TransformPoint(vertices[i]);
        }
        
        return vertices;
    }

    void DrawDebugPoints()
    {
        // Desenha as cápsulas em verde (transparente)
        Gizmos.color = new Color(0, 1, 0, 0.2f);
        foreach (CapsuleCollider capsule in capsuleColliders)
        {
            if (capsule != null)
            {
                DrawCapsuleGizmo(capsule);
            }
        }
        
        // Desenha os pontos vermelhos nos vértices dentro das cápsulas
        Gizmos.color = Color.red;
        foreach (Vector3 vertex in verticesInsideCapsules)
        {
            Gizmos.DrawSphere(vertex, pointSize);
        }
        
        // Desenha o wireframe das cápsulas em verde sólido
        Gizmos.color = Color.green;
        foreach (CapsuleCollider capsule in capsuleColliders)
        {
            if (capsule != null)
            {
                DrawCapsuleWireframe(capsule);
            }
        }
    }

    void DrawCapsuleGizmo(CapsuleCollider capsule)
    {
        Vector3 scale = capsule.transform.lossyScale;
        float scaledRadius = capsule.radius * Mathf.Max(scale.x, scale.z);
        float scaledHeight = capsule.height * scale.y;
        
        Vector3 center = capsule.transform.TransformPoint(capsule.center);
        Vector3 direction = GetCapsuleDirectionWorld(capsule);
        
        float cylinderHeight = scaledHeight - 2 * scaledRadius;
        cylinderHeight = Mathf.Max(cylinderHeight, 0);
        
        Vector3 topSphereCenter = center + direction * (cylinderHeight * 0.5f);
        Vector3 bottomSphereCenter = center - direction * (cylinderHeight * 0.5f);
        
        // Desenha as esferas das extremidades
        Gizmos.DrawSphere(topSphereCenter, scaledRadius);
        Gizmos.DrawSphere(bottomSphereCenter, scaledRadius);
        
        // Desenha o cilindro central
        DrawCylinderGizmo(bottomSphereCenter, topSphereCenter, scaledRadius);
    }

    void DrawCapsuleWireframe(CapsuleCollider capsule)
    {
        Vector3 scale = capsule.transform.lossyScale;
        float scaledRadius = capsule.radius * Mathf.Max(scale.x, scale.z);
        float scaledHeight = capsule.height * scale.y;
        
        Vector3 center = capsule.transform.TransformPoint(capsule.center);
        Vector3 direction = GetCapsuleDirectionWorld(capsule);
        
        float cylinderHeight = scaledHeight - 2 * scaledRadius;
        cylinderHeight = Mathf.Max(cylinderHeight, 0);
        
        Vector3 topSphereCenter = center + direction * (cylinderHeight * 0.5f);
        Vector3 bottomSphereCenter = center - direction * (cylinderHeight * 0.5f);
        
        // Desenha o wireframe das esferas
        Gizmos.DrawWireSphere(topSphereCenter, scaledRadius);
        Gizmos.DrawWireSphere(bottomSphereCenter, scaledRadius);
        
        // Desenha linhas conectando as esferas
        DrawCylinderWireframe(bottomSphereCenter, topSphereCenter, scaledRadius);
    }

    Vector3 GetCapsuleDirectionWorld(CapsuleCollider capsule)
    {
        switch (capsule.direction)
        {
            case 0: // X-Axis
                return capsule.transform.right;
            case 1: // Y-Axis
                return capsule.transform.up;
            case 2: // Z-Axis
                return capsule.transform.forward;
            default:
                return capsule.transform.up;
        }
    }

    void DrawCylinderGizmo(Vector3 start, Vector3 end, float radius)
    {
        Vector3 direction = (end - start).normalized;
        float height = Vector3.Distance(start, end);
        
        // Para simplificar, desenha uma esfera alongada
        Gizmos.matrix = Matrix4x4.TRS(
            (start + end) * 0.5f,
            Quaternion.LookRotation(direction),
            new Vector3(radius * 2, height * 0.5f, radius * 2)
        );
        Gizmos.DrawCube(Vector3.zero, Vector3.one);
        Gizmos.matrix = Matrix4x4.identity;
    }

    void DrawCylinderWireframe(Vector3 start, Vector3 end, float radius)
    {
        // Desenha linhas verticais conectando as esferas
        Vector3 up = Vector3.up;
        Vector3 right = Vector3.right;
        Vector3 forward = Vector3.forward;
        
        // Encontra vetores perpendiculares à direção da cápsula
        Vector3 direction = (end - start).normalized;
        Vector3 perpendicular1 = Vector3.Cross(direction, Vector3.up);
        if (perpendicular1.magnitude < 0.1f)
            perpendicular1 = Vector3.Cross(direction, Vector3.right);
        perpendicular1.Normalize();
        Vector3 perpendicular2 = Vector3.Cross(direction, perpendicular1);
        
        // Desenha 4 linhas ao redor do cilindro
        for (int i = 0; i < 4; i++)
        {
            float angle = i * 90f * Mathf.Deg2Rad;
            Vector3 offset = perpendicular1 * Mathf.Cos(angle) * radius + perpendicular2 * Mathf.Sin(angle) * radius;
            Gizmos.DrawLine(start + offset, end + offset);
        }
    }

    // Métodos úteis para o Inspector
    [ContextMenu("Add All Children BoxColliders")]
    void AddAllChildrenBoxColliders()
    {
        BoxCollider[] childrenColliders = GetComponentsInChildren<BoxCollider>();
        boxCollidersToCheck = childrenColliders;
        Debug.Log($"Adicionados {childrenColliders.Length} BoxColliders dos children");
    }

    [ContextMenu("Clear BoxColliders")]
    void ClearBoxColliders()
    {
        boxCollidersToCheck = new BoxCollider[0];
        Debug.Log("BoxColliders limpos");
    }

    [ContextMenu("Debug Info")]
    void PrintDebugInfo()
    {
        FindAllCapsuleColliders();
        FindVerticesInsideCapsules();
        
        Debug.Log($"Encontrados {capsuleColliders.Count} CapsuleColliders");
        Debug.Log($"Configurados {boxCollidersToCheck?.Length ?? 0} BoxColliders para verificação");
        Debug.Log($"Encontrados {verticesInsideCapsules.Count} vértices dentro das cápsulas");
        
        foreach (Vector3 vertex in verticesInsideCapsules)
        {
            Debug.Log($"Vértice dentro da cápsula: {vertex}");
        }
    }
}