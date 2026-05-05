using UnityEngine;
using System.Collections.Generic;
using System;

public class TrackPreview : MonoBehaviour
{
    [TextArea(10, 20)]
    public string rawData;

    [Header("Gizmos Settings")]
    public bool drawGizmos = true;
    public float gizmoPointSize = 0.15f;
    public Color centerColor = Color.white;
    public Color sideColor = Color.gray;

    [Header("Sequence Settings")]
    [Tooltip("Ponto da lista original que será o 'Ponto 0' na hierarquia")]
    public int startIndex = 0;
    [Tooltip("Pular pontos na geração (1 = gera todos, 10 = gera a cada 10)")]
    public int skipPoints = 1;

    private List<Vector3> centerPoints = new List<Vector3>();
    private List<Vector3> leftPoints = new List<Vector3>();
    private List<Vector3> rightPoints = new List<Vector3>();
    private List<Vector3> smoothedCenter = new List<Vector3>();

    [ContextMenu("1. Processar e Suavizar")]
    public void ProcessAndSmooth()
    {
        ParseRawData();
        smoothedCenter = SmoothPoints(centerPoints);
        Debug.Log($"Dados Processados. Centro Original: {centerPoints.Count} | Suavizado: {smoothedCenter.Count}");
    }

    [ContextMenu("2. Gerar GameObjects (Centro)")]
    public void ExtractCenterPointsAsObjects()
    {
        if (smoothedCenter.Count == 0) ProcessAndSmooth();
        if (smoothedCenter.Count == 0) return;

        string holderName = "Smoothed_Center_Line";
        Transform existing = transform.Find(holderName);
        if (existing) DestroyImmediate(existing.gameObject);

        GameObject holder = new GameObject(holderName);
        holder.transform.SetParent(this.transform);
        holder.transform.localPosition = Vector3.zero;

        int total = smoothedCenter.Count;
        int step = Mathf.Max(1, skipPoints); 

        // O loop corre 'total' vezes, mas o índice real é deslocado pelo startIndex usando Módulo (%)
        for (int i = 0; i < total; i += step)
        {
            int circularIndex = (startIndex + i) % total;
            
            GameObject p = new GameObject($"Point_{i:D3}_(Idx_{circularIndex})");
            p.transform.SetParent(holder.transform);
            p.transform.position = smoothedCenter[circularIndex];
        }
        
        Debug.Log($"Hierarquia gerada com sucesso sob {holderName}!");
    }

    private void ParseRawData()
    {
        centerPoints.Clear();
        leftPoints.Clear();
        rightPoints.Clear();

        string[] lines = rawData.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        string currentCurve = "";

        foreach (string line in lines)
        {
            if (line.StartsWith("CURVE:")) {
                currentCurve = line.Replace("CURVE:", "").Trim().ToLower();
                continue;
            }
            if (string.IsNullOrEmpty(line) || char.IsLetter(line[0])) continue;

            try {
                string[] coords = line.Split(',');
                if (coords.Length >= 3) {
                    float x = float.Parse(coords[0], System.Globalization.CultureInfo.InvariantCulture);
                    float y = float.Parse(coords[1], System.Globalization.CultureInfo.InvariantCulture);
                    float z = float.Parse(coords[2], System.Globalization.CultureInfo.InvariantCulture);
                    
                    // Conversão de coordenadas para o mundo Unity
                    Vector3 point = new Vector3(-x, z, -y);

                    if (currentCurve.Contains("side 1")) leftPoints.Add(point);
                    else if (currentCurve.Contains("central")) centerPoints.Add(point);
                    else if (currentCurve.Contains("side 2")) rightPoints.Add(point);
                }
            } catch { }
        }
    }

    private List<Vector3> SmoothPoints(List<Vector3> points)
    {
        if (points.Count < 3) return new List<Vector3>(points);
        List<Vector3> smoothed = new List<Vector3>();
        for (int i = 0; i < points.Count; i++) {
            if (i == 0 || i == points.Count - 1) {
                smoothed.Add(points[i]);
                continue;
            }
            Vector3 avg = (points[i - 1] + points[i] + points[i + 1]) / 3f;
            smoothed.Add(avg);
        }
        return smoothed;
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos) return;

        DrawCurve(leftPoints, sideColor);
        DrawCurve(rightPoints, sideColor);

        List<Vector3> toDrawCenter = smoothedCenter.Count > 0 ? smoothedCenter : centerPoints;
        
        // Desenha uma esfera verde maior no ponto de início selecionado
        if (toDrawCenter.Count > 0)
        {
            int safeStart = startIndex % toDrawCenter.Count;
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(toDrawCenter[safeStart], gizmoPointSize * 2.5f);
        }

        DrawCurve(toDrawCenter, centerColor);
    }

    private void DrawCurve(List<Vector3> points, Color color)
    {
        if (points == null || points.Count < 2) return;
        Gizmos.color = color;
        for (int i = 0; i < points.Count; i++) {
            Gizmos.DrawSphere(points[i], gizmoPointSize);
            if (i < points.Count - 1)
                Gizmos.DrawLine(points[i], points[i + 1]);
        }
    }
}