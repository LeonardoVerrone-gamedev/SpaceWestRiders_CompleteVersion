using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class PlayerGameplayManager : MonoBehaviour
{
    [Header("Components")]
    [SerializeField] Image panel;

    [SerializeField] TextMeshProUGUI playerPositionText;
    [SerializeField] TextMeshProUGUI bestLapTimeText;
    [SerializeField] TextMeshProUGUI currentLapTimeText;
    [SerializeField] TextMeshProUGUI speedKMH;
    [SerializeField] TextMeshProUGUI nitroAmountCount;
    [SerializeField] public Canvas hudCanvas;
    [SerializeField] Image returnSymbol;

    [Header("RPM Gauge")]
    [SerializeField] RectTransform rpmNeedle;

    [SerializeField] float minNeedleAngle = 180f;
    [SerializeField] float maxNeedleAngle = -45f;
    [SerializeField] float maxNeedleAngleWhenNotInNitro = -22.5f;

    [SerializeField] float maxRPM = 9000f;

    [Header("NOS Gauge")]
    [SerializeField] RectTransform nosNeedle;

    [SerializeField] float minNOSAngle = 132f;
    [SerializeField] float maxNOSAngle = -132f;
    float currentNOSAngle;
    [SerializeField] float nosNeedleSmoothSpeed = 3.75f;

    [SerializeField] RacerStatus racerStatus;
    [SerializeField] SCR_RayBasedCarPhysics carPhysics;

    [Header("Proximity Settings")]
    [SerializeField] GameObject opponentIndicatorPrefab; // Imagem UI com OpponentUIIndicator
    [SerializeField] float detectionTrackDistance = 5.0f; // Distância em unidades de waypoint (ex: 5 segmentos)
    [SerializeField] float maxVisualDistance = 100f; // Distância em metros para escala mínima
    private List<OpponentUIIndicator> indicators = new List<OpponentUIIndicator>();
    private List<RacerStatus> allRacers = new List<RacerStatus>();

    [Header("Dialogue system")]
    [SerializeField] TextMeshProUGUI racerNameText;
    [SerializeField] TextMeshProUGUI DialogueLine;
    [SerializeField] Image TextBox;
    [SerializeField] Image TRacerNameTextBox;
    [SerializeField] Image Portrait;
    private bool isDialogueActive = false;
    public bool IsBusy() => isDialogueActive;

    [Header("Status")]
    float playerSpeed;
    float playerRPM;
    int playerPosition;
    float currentLapTime;
    float bestLapTime;
    int currentLap;

    void Start()
    {
        racerStatus = transform.parent.gameObject.GetComponent<RacerStatus>();

        if (racerStatus != null)
        {
            racerStatus.SetGameplayManager(this);
        }
        carPhysics = transform.parent.gameObject.GetComponent<SCR_RayBasedCarPhysics>();
        InitializeProximityIndicators();

        HideDialogue();
    }
    

    void Update()
    {
        hudCanvas.gameObject.SetActive(racerStatus.isPlayer);

        UpdateRPMGauge();
        UpdateSpeedKMH();
        UpdatePosition();
        UpdateLapTimes();
        UpdateNOSGauge();
        UpdateNitroAmountText();
        UpdateWrongWay();
        UpdateProximityIndicators();
    }

    void UpdateRPMGauge()
    {
        playerRPM = carPhysics.engineRPM;

        float effective_maxNeedleAngle = carPhysics.IsTurboActive() ? maxNeedleAngle : maxNeedleAngleWhenNotInNitro;

        float normalizedRPM = Mathf.Clamp01(playerRPM / maxRPM);

        float needleAngle = Mathf.Lerp(minNeedleAngle, effective_maxNeedleAngle, normalizedRPM);

        rpmNeedle.localRotation = Quaternion.Euler(0f, 0f, needleAngle);
    }

    void UpdateNOSGauge()
    {
        int nos = carPhysics.GetNOSAmount();
        int maxNos = carPhysics.GetMaxNOSAmount();

        float normalizedNOS = Mathf.Clamp01((float)nos / maxNos);

        float targetAngle = Mathf.Lerp(minNOSAngle, maxNOSAngle, normalizedNOS);

        currentNOSAngle = Mathf.Lerp(currentNOSAngle, targetAngle, Time.deltaTime * nosNeedleSmoothSpeed);

        nosNeedle.localRotation = Quaternion.Euler(0f, 0f, currentNOSAngle);
    }

    void UpdateNitroAmountText()
    {
        int nos = carPhysics.GetNOSAmount();
        nitroAmountCount.text = $"0{nos.ToString()} left";
    }

    void UpdateSpeedKMH()
    {
        speedKMH.text = $"{Mathf.RoundToInt(carPhysics.speedKMH).ToString()}KM/h";
    }

    void UpdatePosition()
    {
        playerPositionText.text = $"{racerStatus.gridPosition.ToString()}st";
    }

    public void SetSplitScreen(Rect cameraRect)
    {
        if (panel == null) return;

        RectTransform panelRect = panel.rectTransform;

        panelRect.anchorMin = new Vector2(cameraRect.xMin, cameraRect.yMin);
        panelRect.anchorMax = new Vector2(cameraRect.xMax, cameraRect.yMax);

        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
    }

    void UpdateLapTimes()
    {
        currentLapTimeText.text = FormatTime(racerStatus.currentLapTime);

        if (racerStatus.personalRecord < float.MaxValue)
            bestLapTimeText.text = FormatTime(racerStatus.personalRecord);
    }

    void UpdateWrongWay()
    {
        returnSymbol.gameObject.SetActive(racerStatus.isDrivingWrongWay && (carPhysics.GetThrottleInput() > 0.1f));
    }

    string FormatTime(float time)
    {
        int minutes = Mathf.FloorToInt(time / 60f);
        int seconds = Mathf.FloorToInt(time % 60f);
        int milliseconds = Mathf.FloorToInt((time * 1000f) % 1000f);

        return $"{minutes:00}:{seconds:00}:{milliseconds:000}";
    }

    void InitializeProximityIndicators()
    {
        // Limpa se já houver
        foreach(var ind in indicators) Destroy(ind.gameObject);
        indicators.Clear();

        // Encontra todos os competidores na cena
        allRacers.AddRange(FindObjectsByType<RacerStatus>(FindObjectsSortMode.None));
        
        foreach(var racer in allRacers)
        {
            if(racer == racerStatus) continue; // Pula o próprio jogador

            GameObject go = Instantiate(opponentIndicatorPrefab, panel.transform);
            var indicator = go.GetComponent<OpponentUIIndicator>();
            indicator.targetRacer = racer;
            indicators.Add(indicator);
        }
    }

    void UpdateProximityIndicators()
    {
        foreach(var ind in indicators)
        {
            RacerStatus target = ind.targetRacer;
            
            // 1. Cálculo de Distância via TrackProgress
            float distDiff = racerStatus.TrackProgress - target.TrackProgress;

            // Se o valor for negativo, o alvo está na frente. 
            // Se for muito grande, está longe demais atrás.
            if(distDiff > 0 && distDiff < detectionTrackDistance)
            {
                // 2. Cálculo de Escala (0.25f a 2f)
                // Usamos a distância real para a escala parecer natural em 3D
                float realDist = Vector3.Distance(transform.position, target.transform.position);
                float scale = Mathf.Lerp(2f, 0.25f, realDist / maxVisualDistance);
                scale = Mathf.Clamp(scale, 0.25f, 2f);

                // 3. Cálculo de Posição Lateral (Esquerda/Direita)
                // Transformamos a posição do oponente para o espaço local do jogador
                Vector3 relativePos = transform.InverseTransformPoint(target.transform.position);
                
                // Normalizamos o X (largura da pista aproximada de 10-15 unidades)
                float screenX = Mathf.Clamp(relativePos.x / 10f, -1f, 1f);

                ind.UpdateUI(screenX, scale, true);
            }
            else
            {
                ind.UpdateUI(0, 0, false);
            }
        }
    }

    private Coroutine activeTypewriter;

    public void ShowDialogue(string name, string text)
    {
        if (activeTypewriter != null) StopCoroutine(activeTypewriter);

        isDialogueActive = true;
        
        // Ativa os elementos de UI
        TextBox.gameObject.SetActive(true);
        Portrait.gameObject.SetActive(true);
        racerNameText.gameObject.SetActive(true);
        DialogueLine.gameObject.SetActive(true);
        TRacerNameTextBox.gameObject.SetActive(true);

        racerNameText.text = name;
        activeTypewriter = StartCoroutine(Typewrite(text));
    }

    private System.Collections.IEnumerator Typewrite(string text)
    {
        DialogueLine.text = "";
        foreach (char c in text.ToCharArray())
        {
            DialogueLine.text += c;
            // 0.01s a 0.02s
            yield return new WaitForSeconds(0.008f); 
        }
        activeTypewriter = null;
    }

    public void HideDialogue()
    {
        if (activeTypewriter != null) StopCoroutine(activeTypewriter);
        TextBox.gameObject.SetActive(false);
        Portrait.gameObject.SetActive(false);
        racerNameText.gameObject.SetActive(false);
        DialogueLine.gameObject.SetActive(false);
        TRacerNameTextBox.gameObject.SetActive(false);

        isDialogueActive = false;
    }    
}