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

    public float lastLapTime { get; private set; }
    public float bestLapTime { get; private set; } = float.MaxValue;
    public float personalRecord { get; private set; } = float.MaxValue;
    public float currentLapTime { get; private set; }
    private float lapStartTime;

    public bool isDrivingWrongWay { get; private set; }

    float lastTrackProgress;
    float wrongWayTimer;

    [SerializeField] float wrongWayDetectionDelay = 1.2f;

    [SerializeField] int minLookAhead = 3;
    [SerializeField] int maxLookAhead = 12;

    [SerializeField] float speedForMaxLookAhead = 200f;

    Rigidbody rb;

    void OnEnable()
    {
        SCR_TrackSelectionManager.OnRaceSetupCompleted += StartRace;
        RaceManager.OnRaceEffectiveStart += SetLapCountStart;
    }

    void OnDisable()
    {
        SCR_TrackSelectionManager.OnRaceSetupCompleted -= StartRace;
        RaceManager.OnRaceEffectiveStart -= SetLapCountStart;
    }

    void Start()
    {
        rb = GetComponent<Rigidbody>();
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

        if (isPlayer && RaceManager.Instance != null)
        {
            string circuitName = RaceManager.Instance.GetCurrentCircuitName();
            string personalKey = "PLAYER_BEST_" + circuitName;

            personalRecord = PlayerPrefs.GetFloat(personalKey, float.MaxValue);

            string circuitTimeKey = "CIRCUIT_RECORD_TIME_" + circuitName;
            string circuitHolderKey = "CIRCUIT_RECORD_RACER_" + circuitName;

            float circuitRecord = PlayerPrefs.GetFloat(circuitTimeKey, float.MaxValue);
            string circuitHolder = PlayerPrefs.GetString(circuitHolderKey, "NONE");
        }
    }

    void Update()
    {
        if(RaceManager.Instance.hasActuallyStarted)currentLapTime = Time.time - lapStartTime;

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

        CheckWrongWay();
    }

    public void CountLap()
    {
        lapsCompleted++;

        lapStartTime = Time.time;
        currentLapTime = 0f;

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

    int GetLookAheadWaypoint()
    {
        float speed = rb.linearVelocity.magnitude * 3.6f; // kmh

        float t = Mathf.Clamp01(speed / speedForMaxLookAhead);

        int lookAhead = Mathf.RoundToInt(Mathf.Lerp(minLookAhead, maxLookAhead, t));

        return (currentWaypointIndex + lookAhead) % waypoints.Count;
    }

    public void RegisterLapTime(float lapTime, string circuitName, string racerName)
    {
        lastLapTime = lapTime;

        if (lapTime < bestLapTime)
            bestLapTime = lapTime;

        //------------------------------------------------
        // CIRCUIT RECORD (IA OU PLAYER)
        //------------------------------------------------

        string circuitTimeKey = "CIRCUIT_RECORD_TIME_" + circuitName;
        string circuitHolderKey = "CIRCUIT_RECORD_RACER_" + circuitName;

        float savedCircuitTime = PlayerPrefs.GetFloat(circuitTimeKey, float.MaxValue);

        if (lapTime < savedCircuitTime)
        {
            PlayerPrefs.SetFloat(circuitTimeKey, lapTime);
            PlayerPrefs.SetString(circuitHolderKey, racerName);
            PlayerPrefs.Save();
        }

        //------------------------------------------------
        // PERSONAL RECORD (SÓ PLAYER)
        //------------------------------------------------

        if (isPlayer)
        {
            string personalKey = "PLAYER_BEST_" + circuitName;

            float saved = PlayerPrefs.GetFloat(personalKey, float.MaxValue);

            if (lapTime < saved)
            {
                PlayerPrefs.SetFloat(personalKey, lapTime);
                PlayerPrefs.Save();

                personalRecord = lapTime;
            }
        }
    }

    void SetLapCountStart()
    {
        lapStartTime = Time.time;
        currentLapTime = 0f;

        lastTrackProgress = TrackProgress;
    }

    void CheckWrongWay()
    {
        if (waypoints.Count == 0 || rb.linearVelocity.magnitude < 1f) 
        {
            isDrivingWrongWay = false;
            wrongWayTimer = 0f;
            return;
        }

        // Pega índices anterior, atual e próximo
        int currentIdx = currentWaypointIndex;
        int prevIdx = (currentIdx - 1 + waypoints.Count) % waypoints.Count;
        int nextIdx = (currentIdx + 1) % waypoints.Count;
        
        // Calcula direção da pista usando média ponderada baseada na posição do jogador
        Vector3 toPrev = (waypoints[currentIdx].position - waypoints[prevIdx].position).normalized;
        Vector3 toNext = (waypoints[nextIdx].position - waypoints[currentIdx].position).normalized;
        
        // Descobre em qual parte do segmento o jogador está
        float distToCurrent = Vector3.Distance(transform.position, waypoints[currentIdx].position);
        float segmentLength = Vector3.Distance(waypoints[currentIdx].position, waypoints[nextIdx].position);
        float progress = Mathf.Clamp01(distToCurrent / segmentLength);
        
        // Interpola entre a direção do segmento anterior e próximo
        Vector3 trackDir = Vector3.Lerp(toPrev, toNext, progress).normalized;
        
        // Direção do jogador
        Vector3 velocityDir = rb.linearVelocity.normalized;
        velocityDir.y = 0;
        
        // Produto escalar
        float dot = Vector3.Dot(velocityDir, trackDir);
        
        // Lógica do timer
        if (dot < -0.2f)
        {
            wrongWayTimer += Time.deltaTime;
        }
        else
        {
            wrongWayTimer = Mathf.Max(0, wrongWayTimer - Time.deltaTime * 2f);
        }
        
        isDrivingWrongWay = wrongWayTimer > wrongWayDetectionDelay;
    }

    private PlayerGameplayManager myManager;

    public void SetGameplayManager(PlayerGameplayManager manager) 
    {
        myManager = manager;
    }

    public PlayerGameplayManager GetGameplayManager() => myManager;
}