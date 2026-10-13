
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

    [Tooltip("Perfis alternativos para Record Holder")]
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
    [SerializeField] private TextMeshProUGUI recordTimeText;
    [SerializeField] private TextMeshProUGUI recordHolderText;
    [SerializeField] private TextMeshProUGUI trackName;

    private CircuitButtonData lastButton;
    public string lastButtonName;

    private Coroutine transitionCoroutine;
    private CircuitSO currentCircuit;

    private bool isTransitioning;
    private bool playbackEnded;

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

        //ColocarVideoEmPrimeiroPlano();
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

        CarregarEExibirRecordes(circuit.circuitName);
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

    // RANDOM: sorteia o CircuitSO primeiro para conhecer seu vídeo.
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

        // Sem vídeo: mantém o fluxo de início, sem bloquear a corrida.
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

        // Guarda a posição original antes de mover a RawImage.
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

        // RANDOM: começa no centro, crescendo a partir de zero.
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

        // Movimento e expansão acontecem simultaneamente.
        while (elapsed < transitionDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(
                elapsed / transitionDuration);

            // Ease-out: rápido no início, suave no fim.
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            videoRect.anchoredPosition = Vector2.Lerp(
                startPosition, Vector2.zero, eased);

            videoRect.sizeDelta = Vector2.Lerp(
                startSize, targetSize, eased);

            yield return null;
        }

        videoRect.anchoredPosition = Vector2.zero;
        videoRect.sizeDelta = targetSize;

        // A transição acabou. Agora começa uma reprodução completa.
        videoPlayer.Stop();
        videoPlayer.isLooping = false;
        videoPlayer.time = 0;
        playbackEnded = false;

        videoPlayer.loopPointReached += AoFinalizarVideo;
        videoPlayer.Play();

        yield return new WaitUntil(() => playbackEnded);

        videoPlayer.loopPointReached -= AoFinalizarVideo;

        // Só inicia o circuito depois da última reprodução completa.
        videoPlayer.Stop();

        QuickPlayManagement.Instance.CreateCircuit(
            circuit.circuitID);
    }

    private void AoFinalizarVideo(VideoPlayer source)
    {
        playbackEnded = true;
    }

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

    private void CarregarEExibirRecordes(string circuitName)
    {
        string timeKey = "CIRCUIT_RECORD_TIME_" + circuitName;
        string holderKey = "CIRCUIT_RECORD_RACER_" + circuitName;

        if (trackName != null)
            trackName.text = circuitName;

        float recordTime = PlayerPrefs.GetFloat(
            timeKey, float.MaxValue);

        string recordHolder = PlayerPrefs.GetString(
            holderKey, "NONE");

        if (recordTimeText != null)
        {
            recordTimeText.text =
                recordTime == float.MaxValue
                ? "--:--.__"
                : FormatarTempo(recordTime);
        }

        if (recordHolderText == null)
            return;

        if (recordHolder == "NONE")
        {
            if (backupRacerProfiles != null &&
                backupRacerProfiles.Count > 0)
            {
                List<RacerProfileSO> validProfiles =
                    backupRacerProfiles.FindAll(p => p != null);

                if (validProfiles.Count > 0)
                {
                    string sorteado = validProfiles[
                        Random.Range(0, validProfiles.Count)].racerName;

                    PlayerPrefs.SetString(holderKey, sorteado);
                    PlayerPrefs.Save();

                    recordHolderText.text = sorteado.ToUpper();
                }
                else
                {
                    recordHolderText.text = "DESCONHECIDO";
                }
            }
            else
            {
                recordHolderText.text = "DESCONHECIDO";
            }
        }
        else
        {
            recordHolderText.text = recordHolder.ToUpper();
        }
    }

    private string FormatarTempo(float tempo)
    {
        int minutos = Mathf.FloorToInt(tempo / 60f);
        int segundos = Mathf.FloorToInt(tempo % 60f);
        int milissegundos = Mathf.FloorToInt(
            (tempo * 1000f) % 1000f);

        return string.Format(
            "{0}:{1:00}.{2:000}",
            minutos, segundos, milissegundos);
    }

    void OnDisable()
    {
        lastButton = null;
        lastButtonName = string.Empty;

        if (videoPlayer != null)
            videoPlayer.loopPointReached -= AoFinalizarVideo;
    }

    private void ColocarVideoEmPrimeiroPlano()
    {
        if (videoTransitionOverlay == null || videoRect == null)
            return;

        // O overlay fica acima dos demais elementos da Canvas.
        videoTransitionOverlay.SetAsLastSibling();

        // O vídeo fica acima dos filhos do próprio overlay.
        videoRect.SetAsLastSibling();

        // Garante que a RawImage possa ser renderizada.
        displayCircuitoVideo.raycastTarget = false;
    }
}
