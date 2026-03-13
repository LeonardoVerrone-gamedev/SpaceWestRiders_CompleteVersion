using UnityEngine;
using TMPro;
using UnityEngine.UI;

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


    [Header("Status")]
    float playerSpeed;
    float playerRPM;
    int playerPosition;
    float currentLapTime;
    float bestLapTime;
    int currentLap;

    void Start()
    {
        racerStatus = transform.root.gameObject.GetComponent<RacerStatus>();
        carPhysics = transform.root.gameObject.GetComponent<SCR_RayBasedCarPhysics>();
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
        returnSymbol.gameObject.SetActive(racerStatus.isDrivingWrongWay);
    }

    string FormatTime(float time)
    {
        int minutes = Mathf.FloorToInt(time / 60f);
        int seconds = Mathf.FloorToInt(time % 60f);
        int milliseconds = Mathf.FloorToInt((time * 1000f) % 1000f);

        return $"{minutes:00}:{seconds:00}:{milliseconds:000}";
    }


}