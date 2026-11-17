
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using TMPro;

public class CircuitSelection : MonoBehaviour
{
    [Header("Configurações de Circuitos")]
    public CircuitSO[] allFreeCircuits;

    [Tooltip("Mantido para compatibilidade com referências antigas do Inspector.")]
    public List<RacerProfileSO> backupRacerProfiles;

    [Header("Layouts dos Planetas")]
    public GameObject Planet1Layout;
    public GameObject Planet2Layout;
    public GameObject Planet3Layout;
    public GameObject Planet4Layout;

    private readonly List<GameObject> allLayouts =
        new List<GameObject>();

    [Header("Vídeo da Seleção")]
    [SerializeField] private RawImage displayCircuitoVideo;
    [SerializeField] private RectTransform videoTransitionOverlay;
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private RenderTexture videoRenderTexture;

    [Header("Transição")]
    [SerializeField, Min(0.05f)]
    private float transitionDuration = 0.35f;

    [Header("Exibição de Dados Extras")]
    [SerializeField] private Slider dificuldadeSlider;

    [Header("Global Records")]
    [SerializeField] private TextMeshProUGUI globalLapUsernameText;
    [SerializeField] private TextMeshProUGUI globalLapTimeText;

    [SerializeField] private TextMeshProUGUI top1Text;
    [SerializeField] private TextMeshProUGUI top2Text;
    [SerializeField] private TextMeshProUGUI top3Text;

    [Header("Local Records")]
    [SerializeField] private TextMeshProUGUI localBestRaceText;
    [SerializeField] private TextMeshProUGUI localBestLapText;
    [SerializeField] private TextMeshProUGUI trackName;

    private CircuitButtonData lastButton;
    public string lastButtonName;

    private Coroutine transitionCoroutine;
    private CircuitSO currentCircuit;

    private bool isTransitioning;
    private bool playbackEnded;

    // Identifica a consulta mais recente.
    // Impede que respostas antigas atualizem a tela.
    private int recordRequestVersion = 0;

    private RectTransform videoRect;
    private Transform originalParent;
    private Vector2 originalAnchoredPosition;
    private Vector2 originalSizeDelta;
    private Vector2 originalAnchorMin;
    private Vector2 originalAnchorMax;
    private Vector2 originalPivot;
    private Vector3 originalScale;
    private Quaternion originalRotation;

    void Awake()
    {
        if (displayCircuitoVideo != null)
        {
            videoRect = displayCircuitoVideo.rectTransform;

            originalParent = videoRect.parent;
            originalAnchoredPosition = videoRect.anchoredPosition;
            originalSizeDelta = videoRect.sizeDelta;
            originalAnchorMin = videoRect.anchorMin;
            originalAnchorMax = videoRect.anchorMax;
            originalPivot = videoRect.pivot;
            originalScale = videoRect.localScale;
            originalRotation = videoRect.localRotation;
        }

        if (videoPlayer != null)
        {
            videoPlayer.playOnAwake = false;
            videoPlayer.isLooping = true;
            videoPlayer.renderMode = VideoRenderMode.RenderTexture;

            if (videoRenderTexture != null)
                videoPlayer.targetTexture = videoRenderTexture;

            if (displayCircuitoVideo != null)
                displayCircuitoVideo.texture = videoRenderTexture;
        }
    }

    void Start()
    {
        allLayouts.Clear();

        if (Planet1Layout != null) allLayouts.Add(Planet1Layout);
        if (Planet2Layout != null) allLayouts.Add(Planet2Layout);
        if (Planet3Layout != null) allLayouts.Add(Planet3Layout);
        if (Planet4Layout != null) allLayouts.Add(Planet4Layout);

        if (videoTransitionOverlay != null)
            videoTransitionOverlay.gameObject.SetActive(true);
    }

    // Chamado pelo sistema de foco dos botões.
    public void NotificarNovoBotaoFocado(CircuitButtonData novoBotao)
    {
        if (isTransitioning || novoBotao == null ||
            novoBotao == lastButton)
            return;

        lastButton = novoBotao;
        lastButtonName = novoBotao.circuitID;

        AtualizarDisplayComCircuito(novoBotao.circuitID);
    }

    public void AtualizarDisplayComCircuito(string circuitID)
    {
        CircuitSO circuit = EncontrarCircuito(circuitID);

        if (circuit == null)
            return;

        currentCircuit = circuit;

        if (dificuldadeSlider != null)
            dificuldadeSlider.value = circuit.DifficultyLevel;

        CarregarEExibirRecordes(circuit);

        ExibirVideoEmLoop(circuit);
    }

    private CircuitSO EncontrarCircuito(string circuitID)
    {
        if (allFreeCircuits == null)
            return null;

        foreach (CircuitSO circuit in allFreeCircuits)
        {
            if (circuit != null &&
                (circuit.circuitID == circuitID ||
                 circuit.name == circuitID))
            {
                return circuit;
            }
        }

        return null;
    }

    // =========================================================
    // RECORDES
    // =========================================================

    private void CarregarEExibirRecordes(CircuitSO circuit)
    {
        if (circuit == null)
            return;

        // Cada novo circuito invalida as consultas anteriores.
        recordRequestVersion++;
        int requestVersion = recordRequestVersion;

        string circuitID = circuit.circuitID;

        if (trackName != null)
            trackName.text = circuit.circuitName;

        // 1. RECORDES LOCAIS
        // São carregados imediatamente, sem depender da internet.

        float localRaceTime =
            LocalRaceRecordStorage.GetBestRaceTime(circuitID);

        float localLapTime =
            LocalRaceRecordStorage.GetBestLapTime(circuitID);

        if (localBestRaceText != null)
        {
            localBestRaceText.text =
                $"Melhor corrida: {FormatarTempo(localRaceTime)}";
        }

        if (localBestLapText != null)
        {
            localBestLapText.text =
                $"Melhor volta: {FormatarTempo(localLapTime)}";
        }

        // 2. LIMPA OS RECORDES GLOBAIS ANTIGOS

        LimparRecordesGlobais("LOADING...");

        // 3. CONSULTAS AO SUPABASE

        if (SupabaseGhostService.Instance == null)
        {
            Debug.LogWarning(
                "[CIRCUIT SELECTION] SupabaseGhostService não encontrado."
            );

            ExibirFalhaRecordesGlobais();
            return;
        }

        // Melhor volta global.
        SupabaseGhostService.Instance.GetBestGlobalLapRecord(
            circuitID,
            (record, success) =>
            {
                if (!ConsultaAindaValida(requestVersion, circuitID))
                    return;

                if (!success)
                {
                    if (globalLapUsernameText != null)
                        globalLapUsernameText.text = "UNAVAILABLE";

                    if (globalLapTimeText != null)
                        globalLapTimeText.text = "--:--.---";

                    return;
                }

                if (record == null)
                {
                    if (globalLapUsernameText != null)
                        globalLapUsernameText.text = "NONE";

                    if (globalLapTimeText != null)
                        globalLapTimeText.text = "--:--.---";

                    return;
                }

                string username = ObterUsername(
                    record.username,
                    record.player_name
                );

                if (globalLapUsernameText != null)
                    globalLapUsernameText.text = username.ToUpper();

                if (globalLapTimeText != null)
                    globalLapTimeText.text =
                        FormatarTempo((float)record.lap_time);
            }
        );

        // Top 3 global de corrida.
        SupabaseGhostService.Instance.GetTop3RaceRecords(
            circuitID,
            (records, success) =>
            {
                if (!ConsultaAindaValida(requestVersion, circuitID))
                    return;

                if (!success)
                {
                    ExibirTop3Indisponivel();
                    return;
                }

                ExibirTop3(records);
            }
        );
    }

    private bool ConsultaAindaValida(
        int requestVersion,
        string circuitID)
    {
        return this != null &&
               isActiveAndEnabled &&
               requestVersion == recordRequestVersion &&
               currentCircuit != null &&
               currentCircuit.circuitID == circuitID;
    }

    private void LimparRecordesGlobais(string mensagem)
    {
        if (globalLapUsernameText != null)
            globalLapUsernameText.text = mensagem;

        if (globalLapTimeText != null)
            globalLapTimeText.text = "--:--.---";

        if (top1Text != null)
            top1Text.text = mensagem;

        if (top2Text != null)
            top2Text.text = mensagem;

        if (top3Text != null)
            top3Text.text = mensagem;
    }

    private void ExibirFalhaRecordesGlobais()
    {
        if (globalLapUsernameText != null)
            globalLapUsernameText.text = "UNAVAILABLE";

        if (globalLapTimeText != null)
            globalLapTimeText.text = "--:--.---";

        ExibirTop3Indisponivel();
    }

    private void ExibirTop3Indisponivel()
    {
        if (top1Text != null)
            top1Text.text = "UNAVAILABLE";

        if (top2Text != null)
            top2Text.text = "UNAVAILABLE";

        if (top3Text != null)
            top3Text.text = "UNAVAILABLE";
    }

    private void ExibirTop3(
        List<GlobalRaceRecordRow> records)
    {
        if (top1Text != null)
            top1Text.text = "1. NONE";

        if (top2Text != null)
            top2Text.text = "2. NONE";

        if (top3Text != null)
            top3Text.text = "3. NONE";

        if (records == null || records.Count == 0)
            return;

        if (records.Count > 0 && top1Text != null)
            top1Text.text = FormatarEntradaTop3(1, records[0]);

        if (records.Count > 1 && top2Text != null)
            top2Text.text = FormatarEntradaTop3(2, records[1]);

        if (records.Count > 2 && top3Text != null)
            top3Text.text = FormatarEntradaTop3(3, records[2]);
    }

    private string FormatarEntradaTop3(
        int position,
        GlobalRaceRecordRow record)
    {
        if (record == null)
            return position + ". NONE";

        string username = ObterUsername(
            record.username,
            record.player_name
        );

        return $"{position}. {username.ToUpper()} - " +
               $"{FormatarTempo((float)record.total_time)}";
    }

    private string ObterUsername(
        string username,
        string fallbackName)
    {
        if (!string.IsNullOrWhiteSpace(username))
            return username.Trim();

        if (!string.IsNullOrWhiteSpace(fallbackName))
            return fallbackName.Trim();

        return "UNKNOWN";
    }

    private string FormatarTempo(float tempo)
    {
        if (tempo == float.MaxValue ||
            float.IsNaN(tempo) ||
            float.IsInfinity(tempo) ||
            tempo <= 0f)
        {
            return "--:--.---";
        }

        int minutos = Mathf.FloorToInt(tempo / 60f);
        int segundos = Mathf.FloorToInt(tempo % 60f);
        int milissegundos = Mathf.FloorToInt(
            (tempo * 1000f) % 1000f
        );

        return string.Format(
            "{0:00}:{1:00}.{2:000}",
            minutos,
            segundos,
            milissegundos
        );
    }

    // =========================================================
    // VÍDEO DE PREVIEW
    // =========================================================

    private void ExibirVideoEmLoop(CircuitSO circuit)
    {
        if (videoPlayer == null ||
            displayCircuitoVideo == null ||
            isTransitioning)
            return;

        if (circuit.circuitVideo == null)
        {
            videoPlayer.Stop();
            videoPlayer.clip = null;
            displayCircuitoVideo.enabled = false;

            Debug.LogWarning(
                $"O circuito {circuit.circuitID} não possui vídeo.");
            return;
        }

        displayCircuitoVideo.enabled = true;

        if (videoPlayer.clip == circuit.circuitVideo &&
            videoPlayer.isPlaying)
            return;

        videoPlayer.Stop();
        videoPlayer.clip = circuit.circuitVideo;
        videoPlayer.isLooping = true;
        videoPlayer.time = 0;
        videoPlayer.Prepare();

        StartCoroutine(IniciarPreviewQuandoPreparado());
    }

    private IEnumerator IniciarPreviewQuandoPreparado()
    {
        while (videoPlayer != null &&
               videoPlayer.clip != null &&
               !videoPlayer.isPrepared)
        {
            yield return null;
        }

        if (videoPlayer != null &&
            videoPlayer.isPrepared &&
            !isTransitioning)
        {
            videoPlayer.time = 0;
            videoPlayer.Play();
        }
    }

    // =========================================================
    // RANDOM / PLAY CIRCUIT
    // =========================================================

    public void RandomPlay()
    {
        if (isTransitioning || allFreeCircuits == null ||
            allFreeCircuits.Length == 0)
            return;

        List<CircuitSO> validCircuits = new List<CircuitSO>();

        foreach (CircuitSO circuit in allFreeCircuits)
        {
            if (circuit != null)
                validCircuits.Add(circuit);
        }

        if (validCircuits.Count == 0)
            return;

        CircuitSO selected = validCircuits[
            Random.Range(0, validCircuits.Count)];

        IniciarSelecaoCircuito(selected, true);
    }

    public void PlayCircuit(string circuitID)
    {
        if (isTransitioning)
            return;

        CircuitSO circuit = EncontrarCircuito(circuitID);

        if (circuit == null)
        {
            Debug.LogWarning(
                $"Circuito não encontrado: {circuitID}");
            return;
        }

        IniciarSelecaoCircuito(circuit, false);
    }

    private void IniciarSelecaoCircuito(
        CircuitSO circuit, bool randomSelection)
    {
        if (QuickPlayManagement.Instance == null)
            return;

        if (circuit.circuitVideo == null ||
            videoPlayer == null ||
            displayCircuitoVideo == null ||
            videoTransitionOverlay == null)
        {
            QuickPlayManagement.Instance.CreateCircuit(
                circuit.circuitID);
            return;
        }

        currentCircuit = circuit;
        lastButtonName = circuit.circuitID;

        if (transitionCoroutine != null)
            StopCoroutine(transitionCoroutine);

        transitionCoroutine = StartCoroutine(
            TransicionarEIniciarCircuito(circuit, randomSelection));
    }

    private IEnumerator TransicionarEIniciarCircuito(
        CircuitSO circuit, bool randomSelection)
    {
        isTransitioning = true;
        displayCircuitoVideo.enabled = true;

        videoPlayer.Stop();
        videoPlayer.clip = circuit.circuitVideo;
        videoPlayer.isLooping = true;
        videoPlayer.time = 0;
        videoPlayer.Prepare();

        while (!videoPlayer.isPrepared)
            yield return null;

        videoTransitionOverlay.gameObject.SetActive(true);

        Vector3 startWorldPosition = videoRect.position;
        Vector2 startSize = videoRect.rect.size;

        ColocarVideoEmPrimeiroPlano();

        videoRect.SetParent(videoTransitionOverlay, true);
        videoRect.anchorMin = new Vector2(0.5f, 0.5f);
        videoRect.anchorMax = new Vector2(0.5f, 0.5f);
        videoRect.pivot = new Vector2(0.5f, 0.5f);
        videoRect.localRotation = Quaternion.identity;
        videoRect.localScale = Vector3.one;

        videoRect.anchoredPosition =
            videoTransitionOverlay.InverseTransformPoint(
                startWorldPosition);

        ColocarVideoEmPrimeiroPlano();

        if (randomSelection)
        {
            videoRect.anchoredPosition = Vector2.zero;
            videoRect.sizeDelta = Vector2.zero;
            startSize = Vector2.zero;
        }
        else
        {
            videoRect.sizeDelta = startSize;
        }

        videoPlayer.Play();

        Vector2 targetSize = videoTransitionOverlay.rect.size;
        Vector2 startPosition = videoRect.anchoredPosition;

        float elapsed = 0f;

        while (elapsed < transitionDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(
                elapsed / transitionDuration);

            float eased = 1f - Mathf.Pow(1f - t, 3f);

            videoRect.anchoredPosition = Vector2.Lerp(
                startPosition, Vector2.zero, eased);

            videoRect.sizeDelta = Vector2.Lerp(
                startSize, targetSize, eased);

            yield return null;
        }

        videoRect.anchoredPosition = Vector2.zero;
        videoRect.sizeDelta = targetSize;

        videoPlayer.Stop();
        videoPlayer.isLooping = false;
        videoPlayer.time = 0;
        playbackEnded = false;

        videoPlayer.loopPointReached += AoFinalizarVideo;
        videoPlayer.Play();

        yield return new WaitUntil(() => playbackEnded);

        videoPlayer.loopPointReached -= AoFinalizarVideo;

        videoPlayer.Stop();

        QuickPlayManagement.Instance.CreateCircuit(
            circuit.circuitID);
    }

    private void AoFinalizarVideo(VideoPlayer source)
    {
        playbackEnded = true;
    }

    // =========================================================
    // LAYOUTS
    // =========================================================

    public void ShowOrHidePlanetLayout(GameObject layout)
    {
        if (isTransitioning || layout == null)
            return;

        bool estavaAtivo = layout.activeInHierarchy;

        foreach (GameObject other in allLayouts)
        {
            if (other != null)
                other.SetActive(false);
        }

        layout.SetActive(!estavaAtivo);
    }

    // =========================================================
    // CLEANUP
    // =========================================================

    void OnDisable()
    {
        // Invalida callbacks de consultas ainda pendentes.
        recordRequestVersion++;

        lastButton = null;
        lastButtonName = string.Empty;

        if (videoPlayer != null)
            videoPlayer.loopPointReached -= AoFinalizarVideo;
    }

    private void ColocarVideoEmPrimeiroPlano()
    {
        if (videoTransitionOverlay == null || videoRect == null)
            return;

        videoTransitionOverlay.SetAsLastSibling();
        videoRect.SetAsLastSibling();

        displayCircuitoVideo.raycastTarget = false;
    }
}
