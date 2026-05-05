using UnityEngine;
using System.Collections.Generic;
using System;

public class TrackPreview : MonoBehaviour
{
    [TextArea(10, 20)]
    public string rawData;
    
    [Header("Gizmos")]
    public Color leftColor = Color.blue;
    public Color centerColor = Color.white;
    public Color rightColor = Color.red;
    public bool drawGizmos = true;
    public float gizmoPointSize = 0.2f;
    
    [Header("Track Following")]
    [Range(0.1f, 2f)] public float followSmoothing = 0.5f;
    
    // As três curvas separadas
    private List<Vector3> leftPoints = new List<Vector3>();
    private List<Vector3> centerPoints = new List<Vector3>();
    private List<Vector3> rightPoints = new List<Vector3>();
    
    // Curvas suavizadas
    private List<Vector3> smoothedLeft = new List<Vector3>();
    private List<Vector3> smoothedCenter = new List<Vector3>();
    private List<Vector3> smoothedRight = new List<Vector3>();
    
    // Cache para lookup rápido
    private float[] cumulativeDistances;
    private float totalLength;
    
    // Singleton
    private static TrackPreview _instance;
    public static TrackPreview Instance => _instance;
    
    void Awake()
    {
        if (_instance == null) _instance = this;
        else Destroy(gameObject);
        
        ProcessData();
        SmoothCurves();
        CalculateCumulativeDistances();
    }
    
    [ContextMenu("Processar Dados")]
    public void ProcessData()
    {
        leftPoints.Clear();
        centerPoints.Clear();
        rightPoints.Clear();
        
        string[] lines = rawData.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        string currentCurve = "";
        
        foreach (string line in lines)
        {
            if (line.StartsWith("CURVE:"))
            {
                currentCurve = line.Replace("CURVE:", "").Trim().ToLower();
                continue;
            }
            
            if (string.IsNullOrEmpty(line) || char.IsLetter(line[0])) continue;
            
            try
            {
                string[] coords = line.Split(',');
                if (coords.Length >= 3)
                {
                    float x = float.Parse(coords[0], System.Globalization.CultureInfo.InvariantCulture);
                    float y = float.Parse(coords[1], System.Globalization.CultureInfo.InvariantCulture);
                    float z = float.Parse(coords[2], System.Globalization.CultureInfo.InvariantCulture);
                    
                    Vector3 point = new Vector3(-x, z, -y);
                    
                    if (currentCurve.Contains("side 1"))
                        leftPoints.Add(point);
                    else if (currentCurve.Contains("central"))
                        centerPoints.Add(point);
                    else if (currentCurve.Contains("side 2"))
                        rightPoints.Add(point);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Erro: {line}. {e.Message}");
            }
        }
        
        Debug.Log($"Pista carregada: Left={leftPoints.Count}, Center={centerPoints.Count}, Right={rightPoints.Count}");
    }
    
    private void SmoothCurves()
    {
        smoothedLeft = SmoothPoints(leftPoints);
        smoothedCenter = SmoothPoints(centerPoints);
        smoothedRight = SmoothPoints(rightPoints);
        
        Debug.Log($"Curvas suavizadas: Center={smoothedCenter.Count} pontos");
    }
    
    private List<Vector3> SmoothPoints(List<Vector3> points)
    {
        if (points.Count < 3) return new List<Vector3>(points);
        
        List<Vector3> smoothed = new List<Vector3>();
        
        for (int i = 0; i < points.Count; i++)
        {
            if (i == 0 || i == points.Count - 1)
            {
                smoothed.Add(points[i]);
                continue;
            }
            
            Vector3 avg = (points[i - 1] + points[i] + points[i + 1]) / 3f;
            smoothed.Add(avg);
        }
        
        return smoothed;
    }
    
    private void CalculateCumulativeDistances()
    {
        if (smoothedCenter.Count < 2) return;
        
        cumulativeDistances = new float[smoothedCenter.Count];
        totalLength = 0f;
        
        for (int i = 0; i < smoothedCenter.Count; i++)
        {
            cumulativeDistances[i] = totalLength;
            
            if (i < smoothedCenter.Count - 1)
            {
                totalLength += Vector3.Distance(smoothedCenter[i], smoothedCenter[i + 1]);
            }
        }
        
        Debug.Log($"Comprimento total da pista: {totalLength:F2}m");
    }
    
    /// <summary>
    /// Obtém a amostra da pista na posição do carro, considerando offset lateral
    /// </summary>
    public TrackSample GetTrackSample(Vector3 worldPosition, float approximateProgress = -1f)
    {
        TrackSample sample = new TrackSample();
        
        if (smoothedCenter.Count < 2)
        {
            sample.position = worldPosition;
            sample.up = Vector3.up;
            return sample;
        }
        
        // Encontrar segmento mais próximo na curva central
        int nearestIndex = FindNearestSegment(worldPosition, approximateProgress);
        
        if (nearestIndex >= smoothedCenter.Count - 1)
            nearestIndex = smoothedCenter.Count - 2;
        
        Vector3 p1 = smoothedCenter[nearestIndex];
        Vector3 p2 = smoothedCenter[nearestIndex + 1];
        
        // Projetar a posição do mundo na linha do segmento
        Vector3 projected = ProjectPointOnLine(p1, p2, worldPosition);
        
        // Calcular progresso no segmento
        float segmentLength = Vector3.Distance(p1, p2);
        float distanceToP1 = Vector3.Distance(p1, projected);
        float t = segmentLength > 0 ? distanceToP1 / segmentLength : 0;
        
        // Posição interpolada na curva central
        Vector3 centerPos = Vector3.Lerp(p1, p2, t);
        
        // Interpolar pontos laterais correspondentes
        Vector3 leftPos = GetInterpolatedPoint(smoothedLeft, nearestIndex, t);
        Vector3 rightPos = GetInterpolatedPoint(smoothedRight, nearestIndex, t);
        
        // Calcular offset lateral do carro em relação à pista
        Vector3 carFlat = new Vector3(worldPosition.x, 0, worldPosition.z);
        Vector3 centerFlat = new Vector3(centerPos.x, 0, centerPos.z);
        Vector3 rightFlat = new Vector3(rightPos.x, 0, rightPos.z);
        Vector3 leftFlat = new Vector3(leftPos.x, 0, leftPos.z);
        
        float trackWidth = Vector3.Distance(leftFlat, rightFlat);
        float distanceToCenter = Vector3.Distance(carFlat, centerFlat);
        
        // Determinar se está na esquerda ou direita
        Vector3 toRight = (rightFlat - centerFlat).normalized;
        Vector3 toCar = (carFlat - centerFlat).normalized;
        float side = Vector3.Dot(toCar, toRight);
        
        // Calcular altura baseada na posição lateral (interpolação entre left e right)
        float leftHeight = GetInterpolatedPoint(smoothedLeft, nearestIndex, t).y;
        float rightHeight = GetInterpolatedPoint(smoothedRight, nearestIndex, t).y;
        float centerHeight = centerPos.y;
        
        float lateralT = 0.5f; // centro por padrão
        
        if (trackWidth > 0.01f)
        {
            lateralT = Mathf.Clamp01((distanceToCenter / (trackWidth * 0.5f)) * side + 0.5f);
        }
        
        float groundHeight;
        Vector3 groundNormal;
        
        if (lateralT <= 0.5f)
        {
            float t2 = lateralT / 0.5f;
            groundHeight = Mathf.Lerp(leftHeight, centerHeight, t2);
            groundNormal = CalculateNormal(nearestIndex, t, leftPos, centerPos);
        }
        else
        {
            float t2 = (lateralT - 0.5f) / 0.5f;
            groundHeight = Mathf.Lerp(centerHeight, rightHeight, t2);
            groundNormal = CalculateNormal(nearestIndex, t, centerPos, rightPos);
        }
        
        sample.position = new Vector3(worldPosition.x, groundHeight, worldPosition.z);
        sample.up = groundNormal;
        
        float distAtPoint = cumulativeDistances[nearestIndex] + distanceToP1;
        sample.normalizedDistance = totalLength > 0 ? distAtPoint / totalLength : 0;
        
        return sample;
    }
    
    private Vector3 GetInterpolatedPoint(List<Vector3> points, int segmentIndex, float t)
    {
        if (points.Count < 2) return Vector3.zero;
        
        if (segmentIndex >= points.Count - 1)
            return points[points.Count - 1];
        
        return Vector3.Lerp(points[segmentIndex], points[segmentIndex + 1], t);
    }
    
    private Vector3 CalculateNormal(int segmentIndex, float t, Vector3 leftPos, Vector3 rightPos)
    {
        // Calcular direção da pista (forward)
        Vector3 forward;
        
        if (segmentIndex < smoothedCenter.Count - 1)
        {
            forward = (smoothedCenter[segmentIndex + 1] - smoothedCenter[segmentIndex]).normalized;
        }
        else
        {
            forward = (smoothedCenter[segmentIndex] - smoothedCenter[segmentIndex - 1]).normalized;
        }
        
        forward.y = 0;
        forward.Normalize();
        
        // Calcular vetor lateral (cross product)
        Vector3 lateral = Vector3.Cross(Vector3.up, forward).normalized;
        
        // Calcular inclinação baseada na diferença de altura entre left e right
        float heightDiff = rightPos.y - leftPos.y;
        float trackWidth = Vector3.Distance(leftPos, rightPos);
        float slope = trackWidth > 0 ? heightDiff / trackWidth : 0;
        
        // Normal da superfície (perpendicular à inclinação lateral)
        Vector3 up = new Vector3(-slope * lateral.x, 1f, -slope * lateral.z).normalized;
        
        return up;
    }
    
    private int FindNearestSegment(Vector3 position, float approximateProgress)
    {
        if (approximateProgress >= 0 && approximateProgress < 1f)
        {
            int approxIndex = Mathf.FloorToInt(approximateProgress * smoothedCenter.Count);
            int start = Mathf.Max(0, approxIndex - 50);
            int end = Mathf.Min(smoothedCenter.Count, approxIndex + 50);
            
            int bestIndex = approxIndex;
            float bestDist = Vector3.Distance(position, smoothedCenter[approxIndex]);
            
            for (int i = start; i < end; i++)
            {
                float dist = Vector3.Distance(position, smoothedCenter[i]);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestIndex = i;
                }
            }
            return bestIndex;
        }
        else
        {
            int bestIndex = 0;
            float bestDist = Vector3.Distance(position, smoothedCenter[0]);
            
            for (int i = 1; i < smoothedCenter.Count; i++)
            {
                float dist = Vector3.Distance(position, smoothedCenter[i]);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    bestIndex = i;
                }
            }
            return bestIndex;
        }
    }
    
    private Vector3 ProjectPointOnLine(Vector3 lineStart, Vector3 lineEnd, Vector3 point)
    {
        Vector3 lineDir = (lineEnd - lineStart).normalized;
        float dot = Vector3.Dot(point - lineStart, lineDir);
        return lineStart + lineDir * dot;
    }
    
    public float GetTotalTrackLength() => totalLength;
    
    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;
        
        // Desenhar curva central
        Gizmos.color = centerColor;
        DrawCurve(smoothedCenter.Count > 0 ? smoothedCenter : centerPoints, centerColor);
        
        // Desenhar curva esquerda
        Gizmos.color = leftColor;
        DrawCurve(smoothedLeft.Count > 0 ? smoothedLeft : leftPoints, leftColor);
        
        // Desenhar curva direita
        Gizmos.color = rightColor;
        DrawCurve(smoothedRight.Count > 0 ? smoothedRight : rightPoints, rightColor);
    }
    
    private void DrawCurve(List<Vector3> points, Color color)
    {
        if (points == null || points.Count < 2) return;
        
        Gizmos.color = color;
        for (int i = 0; i < points.Count - 1; i++)
        {
            Gizmos.DrawLine(points[i], points[i + 1]);
        }
        
        // Desenhar pontos
        Gizmos.color = color;
        foreach (Vector3 p in points)
        {
            Gizmos.DrawSphere(p, gizmoPointSize);
        }
    }

    public TrackSample GetNearestTrackPoint(Vector3 worldPosition)
{
    TrackSample result = new TrackSample();
    result.valid = false;
    
    if (smoothedCenter.Count < 2) return result;
    
    // Encontra o ponto mais próximo na curva central
    int nearest = 0;
    float bestDist = float.MaxValue;
    
    for (int i = 0; i < smoothedCenter.Count; i++)
    {
        float dist = Vector3.Distance(worldPosition, smoothedCenter[i]);
        if (dist < bestDist)
        {
            bestDist = dist;
            nearest = i;
        }
    }
    
    result.position = smoothedCenter[nearest];
    result.up = CalculateNormalAtPoint(nearest);
    result.valid = true;
    
    return result;
}

private Vector3 CalculateNormalAtPoint(int index)
{
    if (index <= 0 || index >= smoothedCenter.Count - 1)
        return Vector3.up;
    
    Vector3 prev = smoothedCenter[index - 1];
    Vector3 curr = smoothedCenter[index];
    Vector3 next = smoothedCenter[index + 1];
    
    // Calcula a direção da pista (tangente)
    Vector3 tangent = (next - prev).normalized;
    tangent.y = 0;
    tangent.Normalize();
    
    // Calcula a inclinação baseada na diferença de altura
    float slope = (next.y - prev.y) / Vector3.Distance(prev, next);
    
    // Normal perpendicular à inclinação
    return new Vector3(-slope * tangent.x, 1f, -slope * tangent.z).normalized;
}
}

public struct TrackSample
{
    public Vector3 position;
    public Vector3 up;
    public float normalizedDistance;
    public bool valid;  // ADICIONE ESTA LINHA
}