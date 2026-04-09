using UnityEngine;
using System.Collections.Generic;

public class SCR_WaypointHolder : MonoBehaviour
{
    [Header("Configurações Visuais")]
    [SerializeField] private Color gizmoColor = Color.cyan;
    [SerializeField] private float gizmoRadius = 0.5f;
    [Range(2, 20)] [SerializeField] private int splineResolution = 10; // Suavidade visual

    public List<Transform> waypoints = new List<Transform>();

    [ContextMenu("Atualizar Lista de Waypoints")]
    public void FetchWaypoints()
    {
        waypoints.Clear();
        foreach (Transform child in transform)
        {
            waypoints.Add(child);
        }
    }

    // Retorna a posição na spline baseada em um float (0 a waypoints.Count)
    // Útil para a IA saber onde deve estar na curva
    public Vector3 GetSplinePosition(float t, bool loop = true)
    {
        int count = waypoints.Count;
        if (count < 2) return Vector3.zero;

        int i = Mathf.FloorToInt(t);
        float weight = t - i;

        if (loop)
        {
            i %= count;
        }
        else if (i >= count - 1)
        {
            return waypoints[count - 1].position;
        }

        // Pontos adjacentes para o cálculo (Catmull-Rom precisa de 4 pontos)
        Vector3 p0 = waypoints[ClampIndex(i - 1, count, loop)].position;
        Vector3 p1 = waypoints[ClampIndex(i, count, loop)].position;
        Vector3 p2 = waypoints[ClampIndex(i + 1, count, loop)].position;
        Vector3 p3 = waypoints[ClampIndex(i + 2, count, loop)].position;

        return CatmullRom(p0, p1, p2, p3, weight);
    }

    private int ClampIndex(int index, int count, bool loop)
    {
        if (index < 0) return loop ? count + index : 0;
        if (index >= count) return loop ? index % count : count - 1;
        return index;
    }

    // Equação matemática da Spline de Catmull-Rom
    private Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;

        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3
        );
    }

    private void OnDrawGizmos()
    {
        FetchWaypoints();
        if (waypoints.Count < 2) return;

        Gizmos.color = gizmoColor;

        for (int i = 0; i < waypoints.Count; i++)
        {
            // Desenha os pontos principais
            Gizmos.DrawSphere(waypoints[i].position, gizmoRadius);

            // Desenha os segmentos da Spline
            Vector3 lastPos = waypoints[i].position;
            for (int j = 1; j <= splineResolution; j++)
            {
                float t = i + (j / (float)splineResolution);
                Vector3 currentPos = GetSplinePosition(t);
                Gizmos.DrawLine(lastPos, currentPos);
                lastPos = currentPos;
            }
        }
    }

    public Vector3 GetSplineTangent(float t, bool loop = true)
    {
        float delta = 0.01f; // Passo curto para precisão da tangente
        Vector3 p1 = GetSplinePosition(t, loop);
        Vector3 p2 = GetSplinePosition(t + delta, loop);
        return (p2 - p1).normalized;
    }
}