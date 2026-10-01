//using Cinemachine;
using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

public class F1ExternalCameraManager : MonoBehaviour
{
    public enum FollowMode
    {
        FollowFirst,
        FollowSelected,
        FinishedRace
    }

    [Header("Cameras")]
    [Tooltip("Todas as Cinemachine Virtual Cameras utilizadas pelas câmeras externas.")]
    [SerializeField]
    private List<CinemachineCamera> cameras = new List<CinemachineCamera>();

    [Header("Racers")]
    [Tooltip("Lista de todos os corredores da corrida.")]
    [SerializeField]
    private List<RacerStatus> racers = new List<RacerStatus>();

    [Header("Mode")]
    [SerializeField]
    private FollowMode followMode = FollowMode.FollowFirst;

    [SerializeField]
    private int selectedRacerIndex = 0;

    [Header("Camera Priority")]
    [SerializeField]
    private int activeCameraPriority = 100;

    [SerializeField]
    private int inactiveCameraPriority = 0;

    [Header("Update")]
    [SerializeField]
    private bool updateCameraEveryFrame = true;

    private RacerStatus currentTarget;
    private CinemachineCamera currentCamera;

    [SerializeField]private bool isInUse = false;

    public bool IsInUse => isInUse;


    private void Awake()
    {
        // Modo padrão
        followMode = FollowMode.FollowFirst;
        selectedRacerIndex = 0;

        InitializeCameras();

        SelectFirstRacer();
    }

    private void Start()
    {
        // Pode ser que os RacerStatus ainda não tenham
        // sido configurados no Awake.
        SelectTargetAccordingToMode();
    }

    private void Update()
    {
        if (!updateCameraEveryFrame)
            return;

        if (currentTarget == null)
        {
            SelectTargetAccordingToMode();
            return;
        }

        UpdateCameraTarget();

        UpdateActiveCamera();
    }

    // =========================================================
    // INITIALIZATION
    // =========================================================

    private void InitializeCameras()
    {
        if (cameras == null)
            return;

        foreach (CinemachineCamera cam in cameras)
        {
            if (cam == null)
                continue;

            cam.Priority = inactiveCameraPriority;
        }
    }

    // =========================================================
    // TARGET SELECTION
    // =========================================================

    public void FollowFirst()
    {
        followMode = FollowMode.FollowFirst;
        selectedRacerIndex = 0;

        SelectFirstRacer();
    }

    public void FollowRacer(int racerIndex)
    {
        if (racers == null || racers.Count == 0)
            return;

        if (racerIndex < 0 || racerIndex >= racers.Count)
            return;

        RacerStatus racer = racers[racerIndex];

        if (racer == null)
            return;

        followMode = FollowMode.FollowSelected;
        selectedRacerIndex = racerIndex;

        SetTarget(racer);
    }

    private void SelectFirstRacer()
    {
        if (racers == null || racers.Count == 0)
        {
            currentTarget = null;
            return;
        }

        // Primeiro corredor da lista
        for (int i = 0; i < racers.Count; i++)
        {
            if (racers[i] != null)
            {
                SetTarget(racers[i]);
                return;
            }
        }

        currentTarget = null;
    }

    private void SelectTargetAccordingToMode()
    {
        if (followMode == FollowMode.FollowFirst)
        {
            SelectFirstRacer();
        }
        else
        {
            if (selectedRacerIndex >= 0 &&
                selectedRacerIndex < racers.Count)
            {
                SetTarget(racers[selectedRacerIndex]);
            }
        }
    }

    private void SetTarget(RacerStatus racer)
    {
        if (racer == null)
            return;

        currentTarget = racer;

        UpdateCameraTarget();

        UpdateActiveCamera();
    }

    // =========================================================
    // CAMERA TARGET
    // =========================================================

    private void UpdateCameraTarget()
    {
        if (!isInUse)
        {
            foreach (CinemachineCamera cam in cameras)
            {
                cam.Priority = -1;
            }
        }

        if (currentTarget == null || !isInUse)
            return;

        Transform target = currentTarget.transform;

        foreach (CinemachineCamera cam in cameras)
        {
            if (cam == null)
                continue;

            // Todas as câmeras olham para o mesmo carro.
            cam.LookAt = target;

            // E seguem o mesmo carro.
            cam.Follow = target;
        }
    }

    // =========================================================
    // CAMERA REGION
    // =========================================================

    private void UpdateActiveCamera()
    {
        if (currentTarget == null)
            return;

        if (currentTarget.waypoints == null ||
            currentTarget.waypoints.Count == 0)
        {
            return;
        }

        int waypointIndex = currentTarget.currentWaypointIndex;
        int totalWaypoints = currentTarget.waypoints.Count;

        CinemachineCamera cameraToActivate = null;

        foreach (CinemachineCamera cam in cameras)
        {
            if (cam == null)
                continue;

            F1CameraWaypointRegion region =
                cam.GetComponent<F1CameraWaypointRegion>();

            if (region == null)
                continue;

            if (region.ContainsWaypoint(waypointIndex, totalWaypoints))
            {
                cameraToActivate = cam;
                break;
            }
        }

        if (cameraToActivate != null)
        {
            SetActiveCamera(cameraToActivate);
        }
    }

    private void SetActiveCamera(CinemachineCamera newCamera)
    {
        if (newCamera == null)
            return;

        // Evita ficar reatribuindo prioridade todo frame
        if (currentCamera == newCamera)
            return;

        foreach (CinemachineCamera cam in cameras)
        {
            if (cam == null)
                continue;

            cam.Priority =
                cam == newCamera
                    ? activeCameraPriority
                    : inactiveCameraPriority;
        }

        currentCamera = newCamera;
    }

    // =========================================================
    // PUBLIC INFORMATION
    // =========================================================

    public RacerStatus GetCurrentTarget()
    {
        return currentTarget;
    }

    public CinemachineCamera GetCurrentCamera()
    {
        return currentCamera;
    }

    public FollowMode GetFollowMode()
    {
        return followMode;
    }

    // =========================================================
    // EDITOR / RUNTIME HELPERS
    // =========================================================

    public void RefreshRacers()
    {
        RacerStatus[] foundRacers =
            Object.FindObjectsByType<RacerStatus>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None
            );

        racers = new List<RacerStatus>(foundRacers);

        if (followMode == FollowMode.FollowFirst)
            SelectFirstRacer();
    }

    public void RefreshCameras()
    {
        F1CameraWaypointRegion[] regions =
            GetComponentsInChildren<F1CameraWaypointRegion>(true);

        cameras.Clear();

        foreach (F1CameraWaypointRegion region in regions)
        {
            if (region == null)
                continue;

            CinemachineCamera cam =
                region.GetComponent<CinemachineCamera>();

            if (cam != null)
                cameras.Add(cam);
        }

        InitializeCameras();

        if (currentTarget != null)
            UpdateCameraTarget();
    }

    public bool TryTakeControl(
    RacerStatus racer,
    CameraController playerCameraController)
    {
        if (racer == null || playerCameraController == null)
            return false;

        if (isInUse)
            return false;

        isInUse = true;

        currentTarget = racer;
        followMode = FollowMode.FinishedRace;

        OutputChannels playerChannel =
            playerCameraController.GetOutputChannel();

        foreach (CinemachineCamera cam in cameras)
        {
            if (cam == null)
                continue;

            cam.OutputChannel = playerChannel;
        }

        UpdateCameraTarget();

        if (cameras != null && cameras.Count > 0)
        {
            SetActiveCamera(cameras[0]);
        }

        return true;
    }
}
