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
    [SerializeField] public Canvas hudCanvas;
    [SerializeField] Image returnSymbol;

    [SerializeField] bool useNeedle;

    [Header("RPM Gauge")]
    [SerializeField] RectTransform rpmNeedle;

    [SerializeField] float minNeedleAngle = 180f;
    [SerializeField] float maxNeedleAngle = -45f;
    [SerializeField] float maxNeedleAngleWhenNotInNitro = -22.5f;

    [SerializeField] float maxRPM = 9000f;

    [SerializeField] Animator gearLightPanel;

    [Header("NOS Gauge")]
    [SerializeField] RectTransform nosNeedle;

    [SerializeField] float minNOSAngle = 132f;
    [SerializeField] float maxNOSAngle = -132f;
    float currentNOSAngle;
    [SerializeField] float nosNeedleSmoothSpeed = 3.75f;

    [SerializeField] RacerStatus racerStatus;
    [SerializeField] SCR_RayBasedCarPhysics carPhysics;

    [SerializeField] Animator NOSLightDisplay;

    [Header("Retired")]
    [SerializeField] TextMeshProUGUI RetiredText;

    private bool retiredUIActive = false;

    [Header("Proximity Settings")]
    [SerializeField] GameObject opponentIndicatorPrefab;
    [SerializeField] float detectionTrackDistance = 5.0f;
    [SerializeField] float maxVisualDistance = 100f;
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
    [Header("Race Finish")]
    [SerializeField] private TextMeshProUGUI finishPositionText;
    [SerializeField] private TextMeshProUGUI finishAdvanceText;

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

        if (RetiredText != null)
            RetiredText.gameObject.SetActive(false);

        if (finishPositionText != null)
            finishPositionText.gameObject.SetActive(false);

        if (finishAdvanceText != null)
            finishAdvanceText.gameObject.SetActive(false);
    }

    void Update()
    {
        if (racerStatus.isRetired)
        {
            ShowRetiredUI();
            return;
        }

        hudCanvas.gameObject.SetActive(racerStatus.isPlayer);

        UpdateRPMGauge();
        UpdateSpeedKMH();
        UpdatePosition();
        UpdateLapTimes();
        UpdateNOSGauge();
        UpdateWrongWay();
        UpdateProximityIndicators();
    }

    public void HideFinishPrompt()
    {
        if (finishAdvanceText != null)
            finishAdvanceText.gameObject.SetActive(false);
    }

    public void ShowFinishPosition(int position)
    {
        if (!racerStatus.isPlayer)
            return;

        if (finishPositionText != null)
        {
            finishPositionText.text = $"{position}ª Pst";
            finishPositionText.gameObject.SetActive(true);
        }

        if (finishAdvanceText != null)
        {
            finishAdvanceText.text = "PRESSIONE AVANÇAR";
            finishAdvanceText.gameObject.SetActive(true);
        }
    }

    void ShowRetiredUI()
    {
        if (retiredUIActive)
            return;

        retiredUIActive = true;

        // Desativa os elementos normais da HUD

        if (playerPositionText != null)
            playerPositionText.gameObject.SetActive(false);

        if (bestLapTimeText != null)
            bestLapTimeText.gameObject.SetActive(false);

        if (currentLapTimeText != null)
            currentLapTimeText.gameObject.SetActive(false);

        if (speedKMH != null)
            speedKMH.gameObject.SetActive(false);

        if (returnSymbol != null)
            returnSymbol.gameObject.SetActive(false);

        if (rpmNeedle != null)
            rpmNeedle.gameObject.SetActive(false);

        if (gearLightPanel != null)
            gearLightPanel.gameObject.SetActive(false);

        if (nosNeedle != null)
            nosNeedle.gameObject.SetActive(false);

        if (NOSLightDisplay != null)
            NOSLightDisplay.gameObject.SetActive(false);

        // Desativa os indicadores dos outros carros
        foreach (var indicator in indicators)
        {
            if (indicator != null)
                indicator.gameObject.SetActive(false);
        }

        // Desativa diálogo
        HideDialogue();

        // Ativa SOMENTE o RetiredText
        if (RetiredText != null)
            RetiredText.gameObject.SetActive(true);
    }

    void UpdateRPMGauge()
    {
        if (useNeedle)
        {
            playerRPM = carPhysics.engineRPM;

            float effective_maxNeedleAngle =
                carPhysics.IsTurboActive()
                    ? maxNeedleAngle
                    : maxNeedleAngleWhenNotInNitro;

            float normalizedRPM = Mathf.Clamp01(playerRPM / maxRPM);

            float needleAngle =
                Mathf.Lerp(minNeedleAngle, effective_maxNeedleAngle, normalizedRPM);

            rpmNeedle.localRotation =
                Quaternion.Euler(0f, 0f, needleAngle);
        }
        else
        {
            int currentGear = carPhysics.currentGear;
            gearLightPanel.SetInteger("Current Gear", currentGear);
        }
    }

    void UpdateNOSGauge()
    {
        int nos = carPhysics.GetNOSAmount();

        if (useNeedle)
        {
            int maxNos = carPhysics.GetMaxNOSAmount();

            float normalizedNOS =
                Mathf.Clamp01((float)nos / maxNos);

            float targetAngle =
                Mathf.Lerp(minNOSAngle, maxNOSAngle, normalizedNOS);

            currentNOSAngle =
                Mathf.Lerp(
                    currentNOSAngle,
                    targetAngle,
                    Time.deltaTime * nosNeedleSmoothSpeed
                );

            nosNeedle.localRotation =
                Quaternion.Euler(0f, 0f, currentNOSAngle);
        }
        else
        {
            NOSLightDisplay.SetInteger("NOS Amount", nos);
        }
    }

    void UpdateSpeedKMH()
    {
        speedKMH.text =
            $"{Mathf.RoundToInt(carPhysics.speedKMH)}KM/h";
    }

    void UpdatePosition()
    {
        playerPositionText.text =
            $"{racerStatus.gridPosition}st";
    }

    public void SetSplitScreen(Rect cameraRect)
    {
        if (panel == null) return;

        RectTransform panelRect = panel.rectTransform;

        panelRect.anchorMin =
            new Vector2(cameraRect.xMin, cameraRect.yMin);

        panelRect.anchorMax =
            new Vector2(cameraRect.xMax, cameraRect.yMax);

        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
    }

    void UpdateLapTimes()
    {
        currentLapTimeText.text =
            FormatTime(racerStatus.currentLapTime);

        if (racerStatus.personalRecord < float.MaxValue)
            bestLapTimeText.text =
                FormatTime(racerStatus.personalRecord);
    }

    void UpdateWrongWay()
    {
        returnSymbol.gameObject.SetActive(
            racerStatus.isDrivingWrongWay &&
            (carPhysics.GetThrottleInput() > 0.1f)
        );
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
        foreach (var ind in indicators)
            Destroy(ind.gameObject);

        indicators.Clear();

        allRacers.AddRange(
            FindObjectsByType<RacerStatus>(
                FindObjectsSortMode.None
            )
        );

        foreach (var racer in allRacers)
        {
            if (racer == racerStatus)
                continue;

            GameObject go =
                Instantiate(
                    opponentIndicatorPrefab,
                    panel.transform
                );

            var indicator =
                go.GetComponent<OpponentUIIndicator>();

            indicator.targetRacer = racer;
            indicators.Add(indicator);
        }
    }

    void UpdateProximityIndicators()
    {
        foreach (var ind in indicators)
        {
            RacerStatus target = ind.targetRacer;

            float distDiff =
                racerStatus.TrackProgress -
                target.TrackProgress;

            if (distDiff > 0 &&
                distDiff < detectionTrackDistance)
            {
                float realDist =
                    Vector3.Distance(
                        transform.position,
                        target.transform.position
                    );

                float scale =
                    Mathf.Lerp(
                        2f,
                        0.25f,
                        realDist / maxVisualDistance
                    );

                scale =
                    Mathf.Clamp(
                        scale,
                        0.25f,
                        2f
                    );

                Vector3 relativePos =
                    transform.InverseTransformPoint(
                        target.transform.position
                    );

                float screenX =
                    Mathf.Clamp(
                        relativePos.x / 10f,
                        -1f,
                        1f
                    );

                ind.UpdateUI(
                    screenX,
                    scale,
                    true
                );
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
        if (activeTypewriter != null)
            StopCoroutine(activeTypewriter);

        isDialogueActive = true;

        TextBox.gameObject.SetActive(true);
        Portrait.gameObject.SetActive(true);
        racerNameText.gameObject.SetActive(true);
        DialogueLine.gameObject.SetActive(true);
        TRacerNameTextBox.gameObject.SetActive(true);

        racerNameText.text = name;

        activeTypewriter =
            StartCoroutine(Typewrite(text));
    }

    private System.Collections.IEnumerator Typewrite(string text)
    {
        DialogueLine.text = "";

        foreach (char c in text.ToCharArray())
        {
            DialogueLine.text += c;
            yield return new WaitForSeconds(0.008f);
        }

        activeTypewriter = null;
    }

    public void HideDialogue()
    {
        if (activeTypewriter != null)
            StopCoroutine(activeTypewriter);

        TextBox.gameObject.SetActive(false);
        Portrait.gameObject.SetActive(false);
        racerNameText.gameObject.SetActive(false);
        DialogueLine.gameObject.SetActive(false);
        TRacerNameTextBox.gameObject.SetActive(false);

        isDialogueActive = false;
    }
}