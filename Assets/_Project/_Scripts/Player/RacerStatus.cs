using UnityEngine;
using System.Collections.Generic;

public class RacerStatus : MonoBehaviour
{
    public List<Transform> waypoints;
    public int position{get; private set;}
    public bool isPlayer{get; private set;}

    public int currentWaypointIndex;
    public int lapsCompleted = 0;
    public float distanceToNextWaypoint;

    [SerializeField] public int gridPosition{ get; private set; }

    public float TrackProgress { get; private set; }

    void OnEnable()
    {
        SCR_TrackSelectionManager.OnRaceSetupCompleted += StartRace;
    }

    void OnDisable()
    {
        SCR_TrackSelectionManager.OnRaceSetupCompleted -= StartRace;
    }

    void StartRace()
    {
        AIRacingController AIController = GetComponent<AIRacingController>();
        isPlayer = (AIController == null||AIController.enabled == false);

        waypoints.Clear();
        SCR_WaypointHolder holder = Object.FindFirstObjectByType<SCR_WaypointHolder>();

        if (holder != null)
        {
            // Chama o método para garantir que a lista está populada
            holder.FetchWaypoints(); 
            
            // Retorna a lista exata
            waypoints = holder.waypoints;
        }
    }

    void Update()
    {
        int newIndex = GetClosestWaypointIndex(transform.position);

        currentWaypointIndex = newIndex;

        // Calcula a distância para o próximo ponto (para desempate no Sort)
        int nextIndex = (currentWaypointIndex + 1) % waypoints.Count;
        distanceToNextWaypoint = Vector3.Distance(transform.position, waypoints[nextIndex].position);

        float segmentLength = Vector3.Distance(
        waypoints[currentWaypointIndex].position,
        waypoints[nextIndex].position
        );

        float segmentProgress = 1f - Mathf.Clamp01(distanceToNextWaypoint / segmentLength);

        TrackProgress = 
            (lapsCompleted * waypoints.Count) +
            currentWaypointIndex +
            segmentProgress;
    }

    public void CountLap()
    {
        lapsCompleted++;
        Debug.Log($"{gameObject.name} completou a volta {lapsCompleted}!");
    }

    public void SetGridPosition(int pos)
    {
        position = pos;
        gridPosition = pos;
    }


    public int GetClosestWaypointIndex(Vector3 currentPosition)
    {
        if (waypoints.Count == 0) return -1;

        float closestDistanceSqr = Mathf.Infinity;
        int closestIndex = 0;

        for (int i = 0; i < waypoints.Count; i++)
        {
            // Calcula a distância ao quadrado (evita o cálculo pesado de raiz quadrada)
            Vector3 directionToWaypoint = waypoints[i].position - currentPosition;
            float dSqr = directionToWaypoint.sqrMagnitude;

            if (dSqr < closestDistanceSqr)
            {
                closestDistanceSqr = dSqr;
                closestIndex = i;
            }
        }

        return closestIndex;
    }
}