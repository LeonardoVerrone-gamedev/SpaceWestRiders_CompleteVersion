//using //Cinemachine;
using Unity.Cinemachine;
using UnityEngine;

public class F1CameraWaypointRegion : MonoBehaviour
{
    [Header("Waypoint Region")]
    [Min(0)]
    public int minWaypoint = 0;

    [Min(0)]
    public int maxWaypoint = 0;

    public CinemachineCamera VirtualCamera
    {
        get
        {
            return GetComponent<CinemachineCamera>();
        }
    }

    /// <summary>
    /// Verifica se um waypoint está dentro da região desta câmera.
    /// Também suporta regiões que atravessam o final da pista.
    /// Exemplo:
    /// min = 90
    /// max = 10
    /// significa 90 -> final da pista -> 0 -> 10
    /// </summary>
    public bool ContainsWaypoint(int waypointIndex, int totalWaypoints)
    {
        if (totalWaypoints <= 0)
            return false;

        waypointIndex = ((waypointIndex % totalWaypoints) + totalWaypoints) % totalWaypoints;

        int min = ((minWaypoint % totalWaypoints) + totalWaypoints) % totalWaypoints;
        int max = ((maxWaypoint % totalWaypoints) + totalWaypoints) % totalWaypoints;

        // Região normal
        if (min <= max)
        {
            return waypointIndex >= min && waypointIndex <= max;
        }

        // Região atravessando o final da pista
        return waypointIndex >= min || waypointIndex <= max;
    }
}
