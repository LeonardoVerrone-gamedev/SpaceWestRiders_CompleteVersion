using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class RaceManager : MonoBehaviour
{
    public static RaceManager Instance;
    
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

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if(QuickPlayManagement.Instance != null) totalLaps = QuickPlayManagement.Instance.competition.circuits[0].lapCount;
        if(MiniTournamentManager.Instance != null) totalLaps = MiniTournamentManager.Instance.CurrentState.competition.circuits[MiniTournamentManager.Instance.CurrentState.currentRaceIndex].lapCount;
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
        allRacers = Object.FindObjectsByType<RacerStatus>(FindObjectsSortMode.None).ToList();
        
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
                    Debug.Log($"{racer.name} largou!");
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
            
            Debug.Log($"{racer.name} no CP {checkpointIndex}. Próximo: {racerCheckpointProgress[racer]}");
        }
    }

    void AssignAIGroups()
    {
        var allAIs = allRacers.Where(r => !r.isPlayer).ToList();
        int focusLeaderCount = Mathf.RoundToInt(allAIs.Count * 0.75f);

        for (int i = 0; i < allAIs.Count; i++)
        {
            AIRacingController AI = allAIs[i].GetComponent<AIRacingController>();
            // Se for do primeiro grupo (75%), persegue o líder, senão o trailer
            bool huntLeader = i < focusLeaderCount;
            if(AI.forceHuntLeader) huntLeader = true;
            
            AI.SetHuntingGroup(huntLeader);
        }
    }

    void UpdateRacePositions()
    {
        var sortedList = allRacers.OrderByDescending(r => r.lapsCompleted)
                                .ThenByDescending(r => r.currentWaypointIndex).ToList();
                                //.ThenBy(r => r.distanceToNextWaypoint).ToList();

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
            1 => 25.0f,
            2 => 18.0f,
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

}

public class RaceResultData
{
    public string racerName;
    public int position;
    public float points;
    public bool isPlayer;
}
