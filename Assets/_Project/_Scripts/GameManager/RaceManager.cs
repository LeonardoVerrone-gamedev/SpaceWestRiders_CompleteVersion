using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

public class RaceManager : MonoBehaviour
{
    public static RaceManager Instance;

    public List<Rigidbody> carRBs = new List<Rigidbody>();
    
    [Header("Configurações de Pista")]
    [SerializeField] private List<BoxCollider> officialCheckpoints; // Arraste os 5 triggers aqui na ordem

    [SerializeField] private List<RacerStatus> allRacers = new List<RacerStatus>();
    public RacerStatus HumanLeader { get; private set; }
    public RacerStatus HumanTrailer { get; private set; }

    // Dicionário para rastrear o progresso de cada corredor sem poluir o RacerStatus
    private Dictionary<RacerStatus, int> racerCheckpointProgress = new Dictionary<RacerStatus, int>();

    private Dictionary<RacerStatus, bool> hasStartedFirstLap = new Dictionary<RacerStatus, bool>(); //dicionario para primeira volta

    [SerializeField] private int totalLaps = 3;

    private bool raceFinished = false;
    private List<RacerStatus> finalRanking = new List<RacerStatus>();

    private CircuitSO currentCircuit;

    bool isLastRace = false;

    public int playersReady = 0;
    public static event Action OnDecalSelectionStart;

    public bool hasActuallyStarted;

    private enum AISettingGroup
    {
        Rival,
        Teammate,
        EasyRubber,
        NoRubber
    }

    private struct RubberProfile
    {
        public bool use;
        public float minCatch;
        public float maxCatch;
        public float minWait;
        public float maxWait;
        public bool huntLeader;

        public RubberProfile(bool use, float minCatch, float maxCatch, float minWait, float maxWait, bool huntLeader)
        {
            this.use = use;
            this.minCatch = minCatch;
            this.maxCatch = maxCatch;
            this.minWait = minWait;
            this.maxWait = maxWait;
            this.huntLeader = huntLeader;
        }
    }

    private RubberProfile rivalProfile = new RubberProfile(
        true,
        10f,   // catch começa cedo
        60f,   // boost máximo cedo
        25f,   // quase não espera
        80f,
        true   // sempre caça líder
    );

    private RubberProfile easyProfile = new RubberProfile(
        true,
        40f,
        180f,
        60f,
        250f,
        true
    );

    private RubberProfile noRubberProfile = new RubberProfile(
        false,
        0f, 0f, 0f, 0f,
        true
    );

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if(QuickPlayManagement.Instance != null)
        { 
            currentCircuit = QuickPlayManagement.Instance.competition.circuits[0];
            isLastRace = true;
        }

        if(MiniTournamentManager.Instance != null)
        { 
            currentCircuit = MiniTournamentManager.Instance.CurrentState.competition.circuits[MiniTournamentManager.Instance.CurrentState.currentRaceIndex];
            isLastRace = MiniTournamentManager.Instance.CurrentState.IsFinal;
        }

        if(FullTournamentManager.Instance != null)
        { 
            currentCircuit = FullTournamentManager.Instance.CurrentState.competition.circuits[FullTournamentManager.Instance.CurrentState.currentRaceIndex];
            isLastRace = FullTournamentManager.Instance.CurrentState.IsFinal;
        }

        if(currentCircuit != null) totalLaps = currentCircuit.lapCount;
        
    }

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
        OnDecalSelectionStart.Invoke();

        allRacers = GameObject.FindObjectsByType<RacerStatus>(FindObjectsSortMode.None).ToList();
        
        foreach (var racer in allRacers)
        {
            racerCheckpointProgress.Add(racer, 0);
            hasStartedFirstLap.Add(racer, false);
        }

        AssignAIGroups();
        InvokeRepeating(nameof(UpdateRacePositions), 0.5f, 0.2f);
    }

    // Este método será chamado pelos Checkpoints ou pelo próprio Trigger do Manager
    public void NotifyCheckpoint(RacerStatus racer, BoxCollider checkpointHit)
    {
        int checkpointIndex = officialCheckpoints.IndexOf(checkpointHit);
        if (checkpointIndex == -1) return;

        int currentExpected = racerCheckpointProgress[racer];

        if (checkpointIndex == currentExpected)
        {
            if (checkpointIndex == 0)
            {
                // SÓ conta volta se já tiver passado pelo checkpoint 0 pelo menos uma vez antes
                if (hasStartedFirstLap[racer])
                {
                    racer.CountLap();
                }
                else
                {
                    // É a largada! Marcamos que ele começou a corrida
                    hasStartedFirstLap[racer] = true;
                   // Debug.Log($"{racer.name} largou!");
                }

                racerCheckpointProgress[racer] = 1;
            }
            else
            {
                racerCheckpointProgress[racer]++;

                if (racerCheckpointProgress[racer] >= officialCheckpoints.Count)
                {
                    racerCheckpointProgress[racer] = 0;
                }
            }
            
           // Debug.Log($"{racer.name} no CP {checkpointIndex}. Próximo: {racerCheckpointProgress[racer]}");
        }
    }

   void AssignAIGroups()
    {
        var allAIs = allRacers.Where(r => !r.isPlayer).ToList();
        var humanPlayers = allRacers.Where(r => r.isPlayer).ToList();

        List<RacerStatus> neutralAIs = new List<RacerStatus>();
        int rivalCount = 0;

        foreach (var aiStatus in allAIs)
        {
            AIRacingController ai = aiStatus.GetComponent<AIRacingController>();
            SCR_CarIdentity identity = aiStatus.GetComponent<SCR_CarIdentity>();

            if (ai == null || identity == null || identity.racerData == null)
            {
                neutralAIs.Add(aiStatus);
                continue;
            }

            // --------------------------------------------------
            // RIVAL CHECK
            // --------------------------------------------------
            bool isRival = currentCircuit.rivals != null &&
               currentCircuit.rivals.Contains(identity.racerData);

            if (isRival)
            {
                if (currentCircuit.allowRubberBanding)
                    ApplyRubberProfile(ai, rivalProfile);
                else
                    ApplyRubberProfile(ai, noRubberProfile);

                ai.SetHuntingGroup(true);
                rivalCount++;
                continue;
            }

            // --------------------------------------------------
            // TEAMMATE CHECK
            // --------------------------------------------------
            RacerStatus teammateTarget = null;

            foreach (var player in humanPlayers)
            {
                var playerIdentity = player.GetComponent<SCR_CarIdentity>();
                if (playerIdentity != null &&
                    playerIdentity.racerData != null &&
                    playerIdentity.racerData.team == identity.racerData.team)
                {
                    teammateTarget = player;
                    break;
                }
            }

            if (teammateTarget != null)
            {
                if (isLastRace)
                {
                    // Última corrida: boss
                    ApplyRubberProfile(ai, rivalProfile);
                    ai.SetHuntingGroup(true);
                    rivalCount++;
                }
                else
                {
                    SetupTeammateAI(ai, teammateTarget);
                }

                continue;
            }

            // --------------------------------------------------
            // NEUTRAL
            // --------------------------------------------------
            neutralAIs.Add(aiStatus);
        }

        // --------------------------------------------------
        // EASY = mesmo número de Rivals
        // --------------------------------------------------

        int easyCount = Mathf.Min(rivalCount, neutralAIs.Count);

        for (int i = 0; i < neutralAIs.Count; i++)
        {
            AIRacingController ai = neutralAIs[i].GetComponent<AIRacingController>();

            if (i < easyCount && currentCircuit.allowRubberBanding)
                ApplyRubberProfile(ai, easyProfile);
            else
                ApplyRubberProfile(ai, noRubberProfile);
        }
    }

    void ApplyRubberProfile(AIRacingController ai, RubberProfile profile)
    {
        ai.rubberBandingValues.SetRubberBandingValues(
            profile.use,
            profile.minCatch,
            profile.maxCatch,
            profile.minWait,
            profile.maxWait
        );

        ai.SetHuntingGroup(profile.huntLeader);
    }

    void SetupTeammateAI(AIRacingController ai, RacerStatus playerTarget)
    {
        ai.rubberBandingValues.SetRubberBandingValues(
            true,
            5f,
            80f,
            5f,
            30f
         );

        ai.SetHuntingGroup(true);

        ai.SetPursuitTarget(playerTarget.GetComponent<SCR_RayBasedCarPhysics>());
    }

    void UpdateRacePositions()
    {
        var sortedList = allRacers
            .OrderByDescending(r => r.TrackProgress)
            .ToList();

        var humans = sortedList.Where(r => r.isPlayer).ToList();
        if (humans.Count > 0)
        {
            // Se houver apenas 1 player, Leader e Trailer são o mesmo (o sistema não quebra)
            HumanLeader = humans.First();
            HumanTrailer = (humans.Count > 1) ? humans.Last() : humans.First();
        }

        for (int i = 0; i < sortedList.Count; i++) sortedList[i].SetGridPosition(i + 1);

        CheckRaceEndCondition();
    }

    void CheckRaceEndCondition()
    {
        if (raceFinished) return;

        var humanRacers = allRacers.Where(r => r.isPlayer).ToList();

        if (humanRacers.Count == 0) return;

        bool allHumansFinished = humanRacers.All(r => r.lapsCompleted >= totalLaps);

        if (allHumansFinished)
        {
            FinishRace();
        }
    }

    void FinishRace()
    {
        raceFinished = true;

        CancelInvoke(nameof(UpdateRacePositions));

        var ordered = allRacers
            .OrderBy(r => r.gridPosition)
            .ToList();

        List<RaceResultData> results = new List<RaceResultData>();

        for (int i = 0; i < ordered.Count; i++)
        {
            var racerStatus = ordered[i];

            var identity = racerStatus.GetComponent<SCR_CarIdentity>();

            string racerName = identity != null && identity.racerData != null
                ? identity.racerData.racerName
                : racerStatus.name;

            results.Add(new RaceResultData
            {
                racerName = racerName,
                position = i + 1,
                points = GetPoints(i + 1, ordered[i].gridPosition),
                isPlayer = racerStatus.isPlayer
            });
        }

        RankingManager.Instance?.OpenRanking(results);
    }

    float GetPoints(int position, int raceIndex)
    {
        float basePoints = position switch
        {
            1 => 35.0f,
            2 => 22.0f,
            3 => 15.0f,
            4 => 12.0f,
            5 => 10.0f,
            6 => 8.0f,
            7 => 6.0f,
            8 => 4.0f,
            _ => 0.0f
        };
        
        // TIEBREAKER 1: posição na corrida (0.001 a 0.008)
        float posTieBreaker = position / 1000.0f;
        
        // TIEBREAKER 2: índice da corrida (0.0001 a 0.0020)
        float raceTieBreaker = (raceIndex + 1) / 10000.0f;
        
        return basePoints - posTieBreaker - raceTieBreaker;
    }

    public void AddRigidbodyForRaceStart(Rigidbody rb)
    {
        Debug.Log($"RB {rb.gameObject} adicionado");
        if(!carRBs.Contains(rb))
        {
            carRBs.Add(rb);
        }
    }

    public void AwakeRacers()
    {
        foreach(Rigidbody rb in carRBs)
        {
            rb.isKinematic = false;
            Debug.Log($"RB {rb.gameObject} disparado");
            rb.WakeUp();
        }
        hasActuallyStarted = true;
    }

    public void EndSelectionAndGoToStart()
    {
        playersReady ++;

        var humanPlayers = allRacers.Where(r => r.isPlayer).ToList();

        if(playersReady >= humanPlayers.Count)
        {
            Invoke("AwakeRacers", 3f);
        }
    }
}

[System.Serializable]
public class RaceResultData
{
    public string racerName;
    public int position;
    public float points;
    public bool isPlayer;
}

