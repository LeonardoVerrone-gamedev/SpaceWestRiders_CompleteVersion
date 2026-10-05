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

    // Cache interno de regiões para evitar GetComponent repetitivo no Update
    private readonly List<F1CameraWaypointRegion> cameraRegions = new List<F1CameraWaypointRegion>();

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

    [SerializeField] private bool isInUse = false;

    public bool IsInUse => isInUse;

    private void Awake()
    {
        followMode = FollowMode.FollowFirst;
        selectedRacerIndex = 0;

        CacheRegions();
        InitializeCameras();
        SelectFirstRacer();
    }

    private void Start()
    {
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
    // INITIALIZATION & CACHE
    // =========================================================

    private void CacheRegions()
    {
        cameraRegions.Clear();
        foreach (CinemachineCamera cam in cameras)
        {
            if (cam != null && cam.TryGetComponent(out F1CameraWaypointRegion region))
            {
                cameraRegions.Add(region);
            }
        }
    }

    private void InitializeCameras()
    {
        if (cameras == null)
            return;

        for (int i = 0; i < cameras.Count; i++)
        {
            if (cameras[i] != null)
            {
                cameras[i].Priority = inactiveCameraPriority;
            }
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
        if (racers == null || racerIndex < 0 || racerIndex >= racers.Count)
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
            if (selectedRacerIndex >= 0 && selectedRacerIndex < racers.Count)
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
            for (int i = 0; i < cameras.Count; i++)
            {
                if (cameras[i] != null)
                    cameras[i].Priority = -1;
            }
        }

        if (currentTarget == null || !isInUse)
            return;

        Transform target = currentTarget.transform;

        for (int i = 0; i < cameras.Count; i++)
        {
            CinemachineCamera cam = cameras[i];
            if (cam == null)
                continue;

            cam.LookAt = target;
            cam.Follow = target;
        }
    }

    // =========================================================
    // CAMERA REGION
    // =========================================================

    private void UpdateActiveCamera()
    {
        if (currentTarget == null || currentTarget.waypoints == null || currentTarget.waypoints.Count == 0)
            return;

        int waypointIndex = currentTarget.currentWaypointIndex;
        int totalWaypoints = currentTarget.waypoints.Count;

        CinemachineCamera cameraToActivate = null;

        for (int i = 0; i < cameraRegions.Count; i++)
        {
            F1CameraWaypointRegion region = cameraRegions[i];
            if (region == null)
                continue;

            if (region.ContainsWaypoint(waypointIndex, totalWaypoints))
            {
                cameraToActivate = region.VirtualCamera;
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
        if (newCamera == null || currentCamera == newCamera)
            return;

        for (int i = 0; i < cameras.Count; i++)
        {
            CinemachineCamera cam = cameras[i];
            if (cam == null)
                continue;

            cam.Priority = (cam == newCamera) ? activeCameraPriority : inactiveCameraPriority;
        }

        currentCamera = newCamera;
    }

    // =========================================================
    // PUBLIC INFORMATION
    // =========================================================

    public RacerStatus GetCurrentTarget() => currentTarget;
    public CinemachineCamera GetCurrentCamera() => currentCamera;
    public FollowMode GetFollowMode() => followMode;

    // =========================================================
    // EDITOR / RUNTIME HELPERS
    // =========================================================

    public void RefreshRacers()
    {
        RacerStatus[] foundRacers = Object.FindObjectsByType<RacerStatus>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None
        );

        racers = new List<RacerStatus>(foundRacers);

        if (followMode == FollowMode.FollowFirst)
            SelectFirstRacer();
    }

    public void RefreshCameras()
    {
        // Aloca sem criar lixo excessivo reutilizando a busca estruturada
        F1CameraWaypointRegion[] regions = GetComponentsInChildren<F1CameraWaypointRegion>(true);

        cameras.Clear();
        for (int i = 0; i < regions.Length; i++)
        {
            F1CameraWaypointRegion region = regions[i];
            if (region == null)
                continue;

            CinemachineCamera cam = region.VirtualCamera;
            if (cam != null)
            {
                cameras.Add(cam);
            }
        }

        CacheRegions();
        InitializeCameras();

        if (currentTarget != null)
            UpdateCameraTarget();
    }

    public bool TryTakeControl(RacerStatus racer, CameraController playerCameraController)
    {
        if (racer == null || playerCameraController == null || isInUse)
            return false;

        isInUse = true;
        currentTarget = racer;
        followMode = FollowMode.FinishedRace;

        OutputChannels playerChannel = playerCameraController.GetOutputChannel();

        for (int i = 0; i < cameras.Count; i++)
        {
            CinemachineCamera cam = cameras[i];
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