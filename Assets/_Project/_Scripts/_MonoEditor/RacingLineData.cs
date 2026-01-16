using UnityEngine;
using System.Collections.Generic;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class RacingLineData : MonoBehaviour
{
    public RacingPointData[] racingPoints;
    
    public int PointCount => racingPoints.Length;
    
    public RacingPointData GetPoint(int index)
    {
        return racingPoints[Mathf.Clamp(index, 0, racingPoints.Length - 1)];
    }

    [ContextMenu("Populate Points")]
    public void PopulatePoints()
    {
        racingPoints = GetComponentsInChildren<RacingPointData>();
    }
}