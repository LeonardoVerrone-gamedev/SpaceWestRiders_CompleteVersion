using UnityEngine;
using System.Collections.Generic;

public class SCR_WaypointHolder : MonoBehaviour
{
    [Header("Configurações Visuais")]
    [SerializeField] private Color gizmoColor = Color.cyan;
    [SerializeField] private float gizmoRadius = 0.5f;
    [Range(2, 20)]
    [SerializeField] private int splineResolution = 10;

    [Header("Debug - Regiões das Câmeras")]
    [SerializeField] private bool drawCameraRegions = true;

    [Tooltip("Espessura visual das linhas das regiões das câmeras.")]
    [SerializeField] private float cameraRegionLineWidth = 0.12f;

    [Tooltip("Altura acima dos waypoints para evitar z-fighting.")]
    [SerializeField] private float cameraRegionHeightOffset = 0.15f;

    [Tooltip("Se ativado, mostra o número da câmera no Scene View.")]
    [SerializeField] private bool showCameraLabels = true;

    [Tooltip("Mostra os limites Min/Max de cada câmera.")]
    [SerializeField] private bool showRegionLimits = true;

    [Tooltip("Cores utilizadas caso a câmera não possua uma cor própria.")]
    [SerializeField]
    private Color[] debugCameraColors =
    {
        Color.red,
        Color.blue,
        Color.green,
        Color.yellow,
        Color.magenta,
        Color.cyan,
        new Color(1f, 0.5f, 0f),
        new Color(0.5f, 0f, 1f)
    };

    public List<Transform> waypoints = new List<Transform>();

    private void OnEnable()
    {
        FetchWaypoints();
    }

    [ContextMenu("Atualizar Lista de Waypoints")]
    public void FetchWaypoints()
    {
        waypoints.Clear();

        foreach (Transform child in transform)
        {
            waypoints.Add(child);
        }
    }

    // =========================================================
    // SPLINE
    // =========================================================

    public Vector3 GetSplinePosition(float t, bool loop = true)
    {
        int count = waypoints.Count;

        if (count < 2)
            return Vector3.zero;

        int i = Mathf.FloorToInt(t);
        float weight = t - i;

        if (loop)
        {
            i %= count;

            if (i < 0)
                i += count;
        }
        else if (i >= count - 1)
        {
            return waypoints[count - 1].position;
        }

        Vector3 p0 = waypoints[ClampIndex(i - 1, count, loop)].position;
        Vector3 p1 = waypoints[ClampIndex(i, count, loop)].position;
        Vector3 p2 = waypoints[ClampIndex(i + 1, count, loop)].position;
        Vector3 p3 = waypoints[ClampIndex(i + 2, count, loop)].position;

        return CatmullRom(p0, p1, p2, p3, weight);
    }

    private int ClampIndex(int index, int count, bool loop)
    {
        if (index < 0)
            return loop ? (count + index) % count : 0;

        if (index >= count)
            return loop ? index % count : count - 1;

        return index;
    }

    private Vector3 CatmullRom(
        Vector3 p0,
        Vector3 p1,
        Vector3 p2,
        Vector3 p3,
        float t)
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

    public Vector3 GetSplineTangent(float t, bool loop = true)
    {
        float delta = 0.01f;

        Vector3 p1 = GetSplinePosition(t, loop);
        Vector3 p2 = GetSplinePosition(t + delta, loop);

        return (p2 - p1).normalized;
    }

    // =========================================================
    // GIZMOS
    // =========================================================

    private void OnDrawGizmos()
    {
        FetchWaypoints();

        if (waypoints.Count < 2)
            return;

        // -----------------------------------------------------
        // Linha original da pista
        // -----------------------------------------------------

        DrawBaseSpline();

        // -----------------------------------------------------
        // Regiões das câmeras
        // -----------------------------------------------------

        if (drawCameraRegions)
        {
            DrawCameraRegions();
        }
    }

    private void DrawBaseSpline()
    {
        Gizmos.color = gizmoColor;

        for (int i = 0; i < waypoints.Count; i++)
        {
            if (waypoints[i] == null)
                continue;

            Gizmos.DrawSphere(
                waypoints[i].position,
                gizmoRadius
            );

            Vector3 lastPos = waypoints[i].position;

            for (int j = 1; j <= splineResolution; j++)
            {
                float t =
                    i + (j / (float)splineResolution);

                Vector3 currentPos =
                    GetSplinePosition(t);

                Gizmos.DrawLine(
                    lastPos,
                    currentPos
                );

                lastPos = currentPos;
            }
        }
    }

    // =========================================================
    // CAMERA REGIONS
    // =========================================================

    private void DrawCameraRegions()
    {
        F1CameraWaypointRegion[] regions =
            FindObjectsByType<F1CameraWaypointRegion>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        if (regions == null || regions.Length == 0)
            return;

        for (int cameraIndex = 0;
             cameraIndex < regions.Length;
             cameraIndex++)
        {
            F1CameraWaypointRegion region =
                regions[cameraIndex];

            if (region == null)
                continue;

            Color color =
                GetCameraDebugColor(cameraIndex);

            DrawSingleCameraRegion(
                region,
                color,
                cameraIndex
            );
        }
    }

    private void DrawSingleCameraRegion(
        F1CameraWaypointRegion region,
        Color color,
        int cameraIndex)
    {
        int count = waypoints.Count;

        if (count < 2)
            return;

        int min = NormalizeWaypoint(
            region.minWaypoint,
            count
        );

        int max = NormalizeWaypoint(
            region.maxWaypoint,
            count
        );

        // -----------------------------------------------------
        // Região normal
        // Exemplo:
        // 20 -> 40
        // -----------------------------------------------------

        if (min <= max)
        {
            DrawSplineRange(
                min,
                max,
                color
            );
        }

        // -----------------------------------------------------
        // Região atravessando o final da pista
        //
        // Exemplo:
        // 90 -> 10
        //
        // Desenha:
        // 90 -> 99
        // 0  -> 10
        // -----------------------------------------------------

        else
        {
            DrawSplineRange(
                min,
                count - 1,
                color
            );

            DrawSplineRange(
                0,
                max,
                color
            );
        }

        // -----------------------------------------------------
        // Limites
        // -----------------------------------------------------

        if (showRegionLimits)
        {
            DrawRegionLimit(
                min,
                color
            );

            DrawRegionLimit(
                max,
                color
            );
        }

        // -----------------------------------------------------
        // Label
        // -----------------------------------------------------

#if UNITY_EDITOR

        if (showCameraLabels)
        {
            Vector3 labelPosition =
                GetSplinePosition(
                    GetRegionLabelT(min, max, count)
                );

            labelPosition +=
                Vector3.up * 1.5f;

            UnityEditor.Handles.color = color;

            string cameraName =
                region.gameObject.name;

            string text =
                $"CAM {cameraIndex + 1}\n" +
                $"{cameraName}\n" +
                $"WP {region.minWaypoint} → {region.maxWaypoint}";

            UnityEditor.Handles.Label(
                labelPosition,
                text
            );
        }

#endif
    }

    // =========================================================
    // DESENHA TRECHO DA SPLINE
    // =========================================================

    private void DrawSplineRange(
        int startWaypoint,
        int endWaypoint,
        Color color)
    {
        if (startWaypoint < 0 ||
            endWaypoint < 0 ||
            startWaypoint >= waypoints.Count ||
            endWaypoint >= waypoints.Count)
        {
            return;
        }

        Gizmos.color = color;

        for (int i = startWaypoint;
             i <= endWaypoint;
             i++)
        {
            Vector3 lastPos =
                GetSplinePosition(
                    i
                );

            lastPos +=
                Vector3.up * cameraRegionHeightOffset;

            for (
                int j = 1;
                j <= splineResolution;
                j++)
            {
                float t =
                    i +
                    j / (float)splineResolution;

                Vector3 currentPos =
                    GetSplinePosition(t);

                currentPos +=
                    Vector3.up *
                    cameraRegionHeightOffset;

                Gizmos.DrawLine(
                    lastPos,
                    currentPos
                );

                lastPos = currentPos;
            }
        }
    }

    // =========================================================
    // LIMITE DA REGIÃO
    // =========================================================

    private void DrawRegionLimit(
        int waypointIndex,
        Color color)
    {
        if (waypointIndex < 0 ||
            waypointIndex >= waypoints.Count)
        {
            return;
        }

        Vector3 position =
            waypoints[waypointIndex].position;

        Vector3 tangent =
            GetSplineTangent(
                waypointIndex
            );

        Vector3 perpendicular =
            Vector3.Cross(
                tangent,
                Vector3.up
            ).normalized;

        Gizmos.color = color;

        float size = 3f;

        Gizmos.DrawLine(
            position -
            perpendicular * size,
            position +
            perpendicular * size
        );

        Gizmos.DrawSphere(
            position,
            gizmoRadius * 1.5f
        );
    }

    // =========================================================
    // LABEL POSITION
    // =========================================================

    private float GetRegionLabelT(
        int min,
        int max,
        int count)
    {
        if (min <= max)
        {
            return min +
                   ((max - min) * 0.5f);
        }

        // Região circular
        int length =
            (count - min) + max;

        float middle =
            length * 0.5f;

        float result =
            min + middle;

        if (result >= count)
            result -= count;

        return result;
    }

    // =========================================================
    // UTILS
    // =========================================================

    private int NormalizeWaypoint(
        int index,
        int count)
    {
        if (count <= 0)
            return 0;

        index %= count;

        if (index < 0)
            index += count;

        return index;
    }

    private Color GetCameraDebugColor(
        int index)
    {
        if (debugCameraColors == null ||
            debugCameraColors.Length == 0)
        {
            return Color.white;
        }

        return debugCameraColors[
            index % debugCameraColors.Length
        ];
    }
}