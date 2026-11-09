using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine.InputSystem;
using System.Collections;

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
    private List<RacerStatus> finishedRacers = new List<RacerStatus>();
    private List<RacerStatus> finalRanking = new List<RacerStatus>();

    private CircuitSO currentCircuit;

    bool isLastRace = false;

    public int playersReady = 0;
    public static event Action OnDecalSelectionStart;
    public static event Action OnRaceEffectiveStart;

    public static event Action<RacerStatus, float> OnPlayerRacerFinished;

    private float raceStartTime;

    public bool hasActuallyStarted;

    [SerializeField] private Font countdownFont;
    [SerializeField] private int countdownFontSize = 100;

    private BestRaceRecordResult bestGlobalRecord;

    private bool ghostLookupCompleted = false;
    private bool raceStartRequested = false;

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

    private Dictionary<RacerStatus, float> lapStartTimes = new Dictionary<RacerStatus, float>();

    private bool waitingForFinishConfirmations = false;
    private List<RaceResultData> pendingRankingResults = null;

    private bool waitingForAllRetiredRanking = false;

    [Header("Race Countdown")]
    [SerializeField] private bool showRaceCountdown = true;
    [SerializeField] private int countdownDuration = 4;

    private bool countdownVisible = false;
    private string countdownText = "";
    private float countdownScale = 1f;
    private float countdownAlpha = 1f;
    private Color countdownColor = Color.white;
    private GUIStyle countdownStyle;
    private Coroutine countdownCoroutine;

    void Awake()
    {
        Instance = this;
        
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

        //Debug.Log(PlayerPrefs.GetFloat("PLAYER_BEST_" + currentCircuit.circuitName));
        
    }

    void OnEnable()
    {
        SCR_TrackSelectionManager.OnRaceSetupCompleted += StartRace;
    }

    void OnDisable()
    {
        SCR_TrackSelectionManager.OnRaceSetupCompleted -= StartRace;
    }

    void Update()
    {
        if (!waitingForFinishConfirmations)
            return;

        MonitorFinishConfirmations();
    }

    void MonitorFinishConfirmations()
    {
        if (SCR_PersistentData.Instance == null)
            return;

        var humanRacers = allRacers
            .Where(r => r.isPlayer)
            .ToList();

        foreach (var player in SCR_PersistentData.Instance.players)
        {
            RacerStatus racer = FindRacerForPlayer(player);

            if (racer == null)
                continue;

            // Retired não precisa apertar Avançar.
            if (racer.isRetired)
                continue;

            // Ainda não terminou.
            if (!racer.HasFinishedRace)
                continue;

            // Já confirmou.
            if (racer.HasConfirmedFinish)
                continue;

            if (IsAdvancePressed(player.device))
            {
                racer.ConfirmFinish();

                Debug.Log(
                    $"Player {player.playerIndex} confirmou o fim da corrida."
                );
            }
        }

        bool allHumansConfirmed = humanRacers.All(
            r => r.isRetired ||
                (r.HasFinishedRace && r.HasConfirmedFinish)
        );

        if (allHumansConfirmed)
        {
            OpenPendingRanking();
        }
    }

    void StartRace()
    {
        OnDecalSelectionStart.Invoke();

        allRacers = GameObject.FindObjectsByType<RacerStatus>(FindObjectsSortMode.None).ToList();

        AssignAIGroups();

        InvokeRepeating(nameof(UpdateRacePositions), 0.5f, 0.2f);

        LoadGlobalRecordAndGhost();
    }

    
private void LoadGlobalRecordAndGhost()
{
    ghostLookupCompleted = false;
    raceStartRequested = false;
    bestGlobalRecord = null;

    if (currentCircuit == null)
    {
        Debug.LogError("[GLOBAL RECORD] CircuitSO não encontrado.");
        ghostLookupCompleted = true;
        return;
    }

    if (SupabaseGhostService.Instance == null)
    {
        Debug.LogError("[GLOBAL RECORD] SupabaseGhostService não encontrado.");
        ghostLookupCompleted = true;
        return;
    }

    Debug.Log(
        $"[GLOBAL RECORD] Buscando recorde de " +
        $"{currentCircuit.circuitName} ({currentCircuit.circuitID})"
    );

    SupabaseGhostService.Instance.GetBestRaceRecord(
        currentCircuit.circuitID,
        OnGlobalRecordLoaded
    );
}

private void OnGlobalRecordLoaded(BestRaceRecordResult result)
{
    bestGlobalRecord = result;
    ghostLookupCompleted = true;

    if (result == null || result.record == null)
    {
        Debug.Log(
            "[GLOBAL RECORD] Nenhum recorde global encontrado."
        );
    }
    else
    {
        Debug.Log(
            "[GLOBAL RECORD] RECORD ENCONTRADO!\n" +
            $"Circuito: {result.record.circuit_name}\n" +
            $"Jogador: {result.record.player_name}\n" +
            $"Tempo: {FormatRaceTime(result.record.total_time)}\n" +
            $"Player ID: {result.record.player_id}\n" +
            $"Carro: {result.record.car_character_id}\n" +
            $"Data: {result.record.created_at}\n" +
            $"Record ID: {result.record.id}\n" +
            $"Ghost: {(result.ghost != null ? "OK" : "INDISPONÍVEL")}"
        );
    }

    // Se a contagem regressiva terminou enquanto aguardávamos,
    // retoma a largada.
    if (raceStartRequested)
    {
        AwakeRacers();
    }
}

private string FormatRaceTime(float time)
{
    int minutes = Mathf.FloorToInt(time / 60f);
    int seconds = Mathf.FloorToInt(time % 60f);
    int milliseconds = Mathf.FloorToInt((time * 1000f) % 1000f);

    return $"{minutes:00}:{seconds:00}.{milliseconds:000}";
}


    // Este método será chamado pelos Checkpoints ou pelo próprio Trigger do Manager
    public void NotifyCheckpoint(RacerStatus racer, BoxCollider checkpointHit)
    {
        if (!racerCheckpointProgress.ContainsKey(racer))
            return;

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
                    float lapTime = Time.time - lapStartTimes[racer];
                    lapStartTimes[racer] = Time.time;

                    var identity = racer.GetComponent<SCR_CarIdentity>();

                    string racerName =
                        identity != null && identity.racerData != null
                        ? identity.racerData.racerName
                        : racer.name;

                    racer.RegisterLapTime(
                        lapTime,
                        currentCircuit.circuitName,
                        racerName
                    );

                    // Envia a volta concluída para o ranking global.
                    if (SupabaseGhostService.Instance != null && racer.isPlayer)
                    {
                        SupabaseGhostService.Instance.SubmitLapRecord(
                            currentCircuit.circuitID,
                            currentCircuit.circuitName,
                            racerName,
                            lapTime
                        );
                    }

                    //Chama verificação supabase: melhor volta?

                    racer.CountLap();

                    if (racer.lapsCompleted >= totalLaps)
                    {
                        RegisterFinish(racer);
                    }
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



    void RegisterFinish(RacerStatus racer)
    {
        if (finishedRacers.Contains(racer))
            return;

        finishedRacers.Add(racer);

        // Restaura o registro da posição e o estado de conclusão.
        int finalPosition = finishedRacers.Count;
        racer.SetFinishedPosition(finalPosition);

        Debug.Log(
            $"{racer.name} terminou a corrida em {finalPosition}º lugar."
        );

        // Somente jogadores locais geram recordes globais.
        if (racer.isPlayer)
        {
            float totalTime = Time.time - raceStartTime;

            OnPlayerRacerFinished?.Invoke(racer, totalTime);
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

        //ai.SetPursuitTarget(playerTarget.GetComponent<SCR_RayBasedCarPhysics>());
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

        var humanRacers = allRacers
            .Where(r => r.isPlayer)
            .ToList();

        if (humanRacers.Count == 0)
            return;

        // =========================================================
        // TODOS OS PLAYERS ESTÃO RETIRED
        // =========================================================

        bool allHumansRetired = humanRacers.All(r => r.isRetired);

        if (allHumansRetired)
        {
            if (!waitingForAllRetiredRanking)
            {
                waitingForAllRetiredRanking = true;

                StartCoroutine(OpenRankingAfterAllRetiredDelay());
            }

            return;
        }

        // =========================================================
        // CASO NORMAL:
        // TODOS OS PLAYERS PARTICIPARAM E ENCERRARAM A CORRIDA
        // =========================================================

        bool allHumansFinished = humanRacers.All(
            r => r.isRetired || r.lapsCompleted >= totalLaps
        );

        if (allHumansFinished)
        {
            FinishRace();
        }
    }

    IEnumerator OpenRankingAfterAllRetiredDelay()
    {
        Debug.Log(
            "Todos os jogadores estão retired. " +
            "Abrindo ranking em 3 segundos."
        );

        yield return new WaitForSeconds(3f);

        waitingForAllRetiredRanking = false;

        FinishRace();
}

    void EndRaceByRetire()
    {
        if (raceFinished) return;

        raceFinished = true;

        CancelInvoke(nameof(UpdateRacePositions));

        Debug.Log("Todos os players estão RETIRED. Corrida encerrada.");

        RetireScreenUIManager.Instance?.Open();
    }

    void FinishRace()
    {
        if (raceFinished)
            return;

        raceFinished = true;

        CancelInvoke(nameof(UpdateRacePositions));

        // ============================================
        // CORREDORES QUE TERMINARAM NORMALMENTE
        // ============================================

        var finished = finishedRacers
            .Where(r => !r.isRetired)
            .ToList();

        // ============================================
        // CORREDORES QUE AINDA ESTAVAM NA PISTA
        // ============================================

        var unfinished = allRacers
            .Where(r =>
                !finishedRacers.Contains(r) &&
                !r.isRetired)
            .OrderByDescending(r => r.TrackProgress)
            .ToList();

        // ============================================
        // ORDEM FINAL
        // ============================================

        var finalOrder = new List<RacerStatus>();

        finalOrder.AddRange(finished);
        finalOrder.AddRange(unfinished);

        // Retired ficam por último
        var retired = allRacers
            .Where(r => r.isRetired)
            .ToList();

        finalOrder.AddRange(retired);

        // ============================================
        // GERA RESULTADOS
        // ============================================

        List<RaceResultData> results = new List<RaceResultData>();

        for (int i = 0; i < finalOrder.Count; i++)
        {
            var racerStatus = finalOrder[i];

            var identity =
                racerStatus.GetComponent<SCR_CarIdentity>();

            string racerName =
                identity != null &&
                identity.racerData != null
                    ? identity.racerData.racerName
                    : racerStatus.name;

            bool isRetired = racerStatus.isRetired;

            results.Add(new RaceResultData
            {
                racerName = racerName,
                position = isRetired ? 0 : i + 1,
                points = isRetired
                    ? 0f
                    : GetPoints(
                        i + 1,
                        racerStatus.gridPosition,
                        racerStatus
                    ),
                isPlayer = racerStatus.isPlayer,
                isRetired = isRetired
            });
        }

        pendingRankingResults = results;

        waitingForFinishConfirmations = true;

        bool allHumansRetired = allRacers
            .Where(r => r.isPlayer)
            .All(r => r.isRetired);

        if (allHumansRetired)
        {
            waitingForFinishConfirmations = false;

            RankingManager.Instance?.OpenRanking(
                pendingRankingResults
            );

            pendingRankingResults = null;

            return;
        }

        Debug.Log(
            "Todos os jogadores humanos terminaram. " +
            "Aguardando confirmação para abrir o ranking."
        );
    }

    float GetPoints(int position, int raceIndex, RacerStatus racer)
    {
        float basePoints = position switch
        {
            1 => 35f,
            2 => 22f,
            3 => 15f,
            4 => 12f,
            5 => 10f,
            6 => 8f,
            7 => 6f,
            8 => 4f,
            _ => 0f
        };

        float lapTieBreaker = 0f;

        if (racer.bestLapTime < float.MaxValue)
        {
            lapTieBreaker = Mathf.Clamp(10f - racer.bestLapTime, 0f, 0.009f);
        }

        float raceTieBreaker = (raceIndex + 1) / 10000f;

        return basePoints - lapTieBreaker - raceTieBreaker;
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
        // Evita iniciar a corrida mais de uma vez.
        if (hasActuallyStarted)
            return;

        // Aguarda a consulta ao Supabase antes de liberar a largada.
        if (!ghostLookupCompleted)
        {
            raceStartRequested = true;

            Debug.Log(
                "[GLOBAL GHOST] Aguardando consulta ao Supabase..."
            );

            return;
        }

        raceStartRequested = false;

        // Remove referências inválidas.
        carRBs.RemoveAll(rb => rb == null);

        // Libera a física dos veículos.
        foreach (Rigidbody rb in carRBs)
        {
            rb.isKinematic = false;
            rb.WakeUp();
        }

        // Limpa os dados anteriores de checkpoint e volta.
        racerCheckpointProgress.Clear();
        hasStartedFirstLap.Clear();
        lapStartTimes.Clear();

        // Inicializa o acompanhamento de todos os corredores.
        foreach (var racer in allRacers)
        {
            if (racer == null)
                continue;

            racerCheckpointProgress[racer] = 0;
            hasStartedFirstLap[racer] = false;
            lapStartTimes[racer] = Time.time;
        }

        // Marca o início oficial da corrida.
        raceStartTime = Time.time;
        hasActuallyStarted = true;

        // Inicia a gravação dos Ghost Cars.
        // O evento deve ser disparado somente uma vez.
        // Instancia e inicia o Ghost Car do recorde global.
        if (bestGlobalRecord != null && bestGlobalRecord.ghost != null)
        {
            if (GhostPrefabDatabase.Instance != null)
            {
                GhostPrefabDatabase.Instance.SpawnAndPlay(
                    bestGlobalRecord.ghost
                );

                Debug.Log("[GLOBAL GHOST] Ghost Car instanciado!");
            }
            else
            {
                Debug.LogWarning(
                    "[GLOBAL GHOST] GhostPrefabDatabase não encontrado."
                );
            }
        }
        else
        {
            Debug.Log("[GLOBAL GHOST] Nenhum ghost disponível para esta pista.");
        }

        // Inicia a gravação dos jogadores.
        OnRaceEffectiveStart?.Invoke();

        Debug.Log("[RACE] Corrida iniciada! Gravação dos Ghost Cars ativada.");
    }


    public void EndSelectionAndGoToStart()
    {
        playersReady++;

        var humanPlayers = allRacers
            .Where(r => r.isPlayer)
            .ToList();

        SCR_ArcadeRaceAudioManager.Instance?.InitializeAudioSystem();

        if (playersReady >= humanPlayers.Count)
        {
            List<CameraController> cams =
                GameObject.FindObjectsByType<CameraController>(
                    FindObjectsSortMode.None
                ).ToList();

            foreach (CameraController cam in cams)
            {
                cam.ExitSelectionCam();
            }

            if (showRaceCountdown)
            {
                if (countdownCoroutine != null)
                    StopCoroutine(countdownCoroutine);

                countdownCoroutine = StartCoroutine(
                    RaceCountdownCoroutine()
                );
            }
            else
            {
                Invoke(nameof(AwakeRacers), 3f);
            }
        }
    }

    private IEnumerator RaceCountdownCoroutine()
    {
        countdownVisible = true;

        float elapsed = 0f;
        float totalDuration = Mathf.Max(1, countdownDuration);

        string[] sequence = { "3", "2", "1", "GO!" };

        while (elapsed < totalDuration)
        {
            elapsed += Time.deltaTime;

            float progress = Mathf.Min(
                elapsed / totalDuration, 0.9999f
            );

            // Divide os 4 segundos em 4 etapas.
            int step = Mathf.Min(
                Mathf.FloorToInt(progress * 4f), 3
            );

            float stepProgress = progress * 4f - step;

            countdownText = sequence[step];

            // Cada número entra com um impacto e depois estabiliza.
            float punch = 1f + 0.65f *
                Mathf.Exp(-stepProgress * 10f) *
                Mathf.Sin(stepProgress * 18f);

            countdownScale = Mathf.Lerp(
                0.65f, 1f,
                Mathf.SmoothStep(0f, 1f,
                    Mathf.Clamp01(stepProgress * 5f))
            ) * punch;

            // Fade curto no início e no final de cada etapa.
            float fadeIn = Mathf.Clamp01(stepProgress * 8f);
            float fadeOut = Mathf.Clamp01(
                (1f - stepProgress) * 7f
            );

            countdownAlpha = Mathf.Min(fadeIn, fadeOut);

            // GO! ganha uma cor especial.
            countdownColor = step == 3
                ? new Color(0.2f, 1f, 0.45f)
                : new Color(1f, 0.82f, 0.25f);

            yield return null;
        }

        countdownVisible = false;
        countdownCoroutine = null;

        // A corrida começa exatamente ao terminar a contagem.
        AwakeRacers();
    }

    private void OnGUI()
    {
        if (!countdownVisible)
            return;

        if (countdownStyle == null)
        {
            countdownStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = 100,
                richText = false
            };
        }

        countdownStyle.font = countdownFont;
        countdownStyle.fontSize = countdownFontSize;

        Matrix4x4 previousMatrix = GUI.matrix;
        Color previousColor = GUI.color;

        Vector2 center = new Vector2(
            Screen.width * 0.5f,
            Screen.height * 0.5f
        );

        // Aumenta e diminui o texto sem mover seu centro.
        GUI.matrix = Matrix4x4.TRS(
            center,
            Quaternion.identity,
            Vector3.one * countdownScale
        );

        countdownStyle.fontSize = 100;

        GUI.color = new Color(
            0f, 0f, 0f, countdownAlpha
        );

        // Sombra para destacar o texto.
        GUI.Label(
            new Rect(-298f, -48f, 600f, 100f),
            countdownText,
            countdownStyle
        );

        GUI.Label(
            new Rect(-302f, -52f, 600f, 100f),
            countdownText,
            countdownStyle
        );

        GUI.color = new Color(
            countdownColor.r,
            countdownColor.g,
            countdownColor.b,
            countdownAlpha
        );

        GUI.Label(
            new Rect(-300f, -50f, 600f, 100f),
            countdownText,
            countdownStyle
        );

        GUI.matrix = previousMatrix;
        GUI.color = previousColor;
    }

    public string GetCurrentCircuitName()
    {
        return currentCircuit != null ? currentCircuit.circuitName : "UNKNOWN";
    }

    public string GetCurrentCircuitID()
    {
        return currentCircuit != null
            ? currentCircuit.circuitID
            : "";
    }

    bool IsAdvancePressed(InputDevice device)
    {
        if (device == null)
            return false;

        if (device is Keyboard keyboard)
        {
            return
                keyboard.enterKey.wasPressedThisFrame ||
                keyboard.spaceKey.wasPressedThisFrame;
        }

        if (device is Gamepad gamepad)
        {
            return gamepad.buttonSouth.wasPressedThisFrame;
        }

        return false;
    }

    RacerStatus FindRacerForPlayer(PlayerSessionData player)
    {
        if (player == null)
            return null;

        foreach (var racer in allRacers)
        {
            var identity =
                racer.GetComponent<SCR_CarIdentity>();

            if (identity == null ||
                identity.racerData == null)
                continue;

            if (identity.racerData.characterID ==
                player.selectedCharacterID)
            {
                return racer;
            }
        }

        return null;
    }

    void OpenPendingRanking()
    {
        if (!waitingForFinishConfirmations)
            return;

        waitingForFinishConfirmations = false;

        Debug.Log("Todos os jogadores confirmaram. Abrindo ranking.");

        RankingManager.Instance?.OpenRanking(
            pendingRankingResults
        );

        pendingRankingResults = null;
    }
}

[System.Serializable]
public class RaceResultData
{
    public string racerName;
    public int position;
    public float points;
    public bool isPlayer;
    public bool isRetired;
}

