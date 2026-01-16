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

    void Awake() => Instance = this;

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
        }

        AssignAIGroups();
        InvokeRepeating(nameof(UpdateRacePositions), 0.5f, 0.2f);
    }

    // Este método será chamado pelos Checkpoints ou pelo próprio Trigger do Manager
    public void NotifyCheckpoint(RacerStatus racer, BoxCollider checkpointHit)
    {
        int checkpointIndex = officialCheckpoints.IndexOf(checkpointHit);
        if (checkpointIndex == -1) return;

        int nextExpected = racerCheckpointProgress[racer];

        if (checkpointIndex == nextExpected)
        {
            // Se bateu no último checkpoint da lista
            if (nextExpected == officialCheckpoints.Count - 1)
            {
                racer.CountLap(); // Chama o método no RacerStatus para somar volta
                racerCheckpointProgress[racer] = 0; // Reseta para o primeiro
            }
            else
            {
                racerCheckpointProgress[racer]++;
            }
        }
    }

    void AssignAIGroups()
    {
        var allAIs = allRacers.Where(r => !r.isPlayer).ToList();
        int focusLeaderCount = Mathf.RoundToInt(allAIs.Count * 0.75f);

        for (int i = 0; i < allAIs.Count; i++)
        {
            // Se for do primeiro grupo (75%), persegue o líder, senão o trailer
            bool huntLeader = i < focusLeaderCount;
            allAIs[i].GetComponent<AIRacingController>().SetHuntingGroup(huntLeader);
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
    }
}