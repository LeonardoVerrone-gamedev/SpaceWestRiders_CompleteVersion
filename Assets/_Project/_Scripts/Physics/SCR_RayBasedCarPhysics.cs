using UnityEngine;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;
using System.Collections;
using Unity.VisualScripting;
using System;

// Requer que o GameObject tenha um Rigidbody
[RequireComponent(typeof(Rigidbody))]
public class SCR_RayBasedCarPhysics : MonoBehaviour
{
    // ======================================================
    // CORE IDENTIFICATION
    // ======================================================

    public CarType carType;
    private bool isHover;
    public bool canSwitchType = false;
    [HideInInspector] public bool AIControlled = false;

    RacerStatus racerStatus;

    // ======================================================
    // BASIC COMPONENT REFERENCES
    // ======================================================

    #region Basic Components

    [Header("Basic Components References")]
    [SerializeField] Rigidbody rb;
    [SerializeField] LayerMask drivable;
    [SerializeField] Transform accelerationPoint;
    [SerializeField] Transform carBody;
    [SerializeField] GameObject[] tires = new GameObject[4];
    [SerializeField] GameObject[] frontTiresParent = new GameObject[2];

    AIRacingController aiDriver;

    #endregion

    // ======================================================
    // BASIC SETUP / CONSTANTS
    // ======================================================

    #region Basic Setup

    [HideInInspector][SerializeField] private static int MIN_WHEELS_TO_CONSIDERE_GROUNDED = 2;

    #endregion

    // ======================================================
    // SUSPENSION & GROUNDING SYSTEM
    // ======================================================

    #region Suspension System

    [Header("Suspension System")]
    [SerializeField] Transform[] rayPoints;
    [SerializeField] float restLenght;
    [SerializeField] float hoverDistance = 1.5f;
    [SerializeField] float springTravel;

    [HideInInspector][SerializeField] float springStiffness;
    [SerializeField] float wheelRadius;
    [HideInInspector][SerializeField] float hoverDamper = 8000f;
    [HideInInspector][SerializeField] float classicDamper = 3500f;

    private float currentTargetSuspensionLength;
    private float currentDamper;

    [SerializeField] private int[] wheelsGrounded = new int[4];
    [SerializeField] private bool isGrounded = false;
    bool wasGrounded;
    float airTime = 0f;

    #endregion

    #region Raycast system

    struct GroundSensor
    {
        public bool hit;
        public RaycastHit hitInfo;
    }

    private GroundSensor[] groundSensors;
    private Vector3 cachedSurfaceNormal = Vector3.up;
    private float cachedAverageHeight;
    private int cachedGroundedCount;

    private int sensorFrameCounter = 0;
    [SerializeField] int aiSensorFrequency = 3;

    private RaycastHit[] _raycastBuffer = new RaycastHit[1];



    #endregion

    // ======================================================
    // DOWNFORCE / AERO (HOVER)
    // ======================================================

    #region Hover Downforce

    [Header("Hover Downforce (Aero)")]
    [HideInInspector] [SerializeField] private float hoverDownforceAmount = 2500f;
    [HideInInspector] [SerializeField] private float minHeightThreshold = 0.4f;
    [HideInInspector] [SerializeField] private bool useDynamicDownforce = true;

    #endregion

    // ======================================================
    // AERODYNAMICS (AIR DRAG)
    // ======================================================

    #region Aerodynamics

    [Header("Aerodynamics (Air Drag)")]
    [SerializeField] float hoverAirDrag = 0.25f;
    [SerializeField] float classicAirDrag = 0.08f;
    [HideInInspector] [SerializeField] float dragThreshold = 50f;
    private float currentAirDrag;

    [SerializeField] private float downforceAmount = 500f;

    #endregion

    // ======================================================
    // CAR PHYSICS SETTINGS
    // ======================================================

    #region Car Settings

    [Header("Car Settings")]
    [SerializeField] float classicCarAcceleration = 25f;
    [SerializeField] float hoverCarAcceleration = 15f;
    [SerializeField] float acceleration = 25f;

    [SerializeField] float classicCarMaxSpeed = 100f;
    [SerializeField] float hoverCarMaxSpeed = 120f;
    [SerializeField] float maxSpeed = 100f;

    [SerializeField] float deceleration = 10f;
    [SerializeField] float steerStrenght = 15f;

    [SerializeField] float classicCarDragCoefficient = 10f;
    [SerializeField] float hoverCarDragCoefficient = 2f;

    [HideInInspector][SerializeField] float dragCoefficient = 1f;

    [HideInInspector] [SerializeField] float airControlStrength;

    #endregion

    // ======================================================
    // BALANCING CURVES
    // ======================================================

    #region Balancing Curves
    [SerializeField] private AnimationCurve hoverCarTurningCurve;
    [SerializeField] private AnimationCurve classicCarTurningCurve;
    [HideInInspector][SerializeField] AnimationCurve turningCurve;

    #endregion

    // ======================================================
    // DRIFT SYSTEM
    // ======================================================

    #region Drift System

    [Header("Drift System Settings")]
    [HideInInspector] [SerializeField] private float maxDriftAngle = 45f;
    [HideInInspector] [SerializeField] private float driftBoostForce = 50f;
    [HideInInspector] [SerializeField] private float driftBoostDuration = 1f;

    [HideInInspector][SerializeField] private float driftEnterThreshold = 0.3f;
    [HideInInspector][SerializeField] private float driftStability = 0.8f;
    [HideInInspector][SerializeField] private float driftAccelerationMultiplier = 1.2f;
    [HideInInspector][SerializeField] private float maxDriftBoostSpeed = 150f;
    [HideInInspector][SerializeField] float speedMaintainForceMultiplier = .75f;
    [HideInInspector][SerializeField] private float driftSelfSteerStrength = 5f;
    [HideInInspector][SerializeField] private float driftCounterSteerLimit = 0.5f;
    [HideInInspector][SerializeField] private float dragRestoreDuration = 0.5f;

    private float _dragRestoreTimer = 0f;
    private bool _isRestoringDrag = false;

    // Drift State
    private bool _isDrifting = false;
    private float _currentDriftAngle = 0f;
    private float _driftBoostTimer = 0f;
    private bool _isDriftBoostActive = false;
    private float _originalDragCoefficient;
    private float _originalMaxSpeed;

    #endregion

    // ======================================================
    // PUBLIC GETTERS (STATE ACCESS)
    // ======================================================

    #region Public Getters - Drift & State

    [HideInInspector] public bool IsDrifting() => _isDrifting;
    [HideInInspector] public float OriginalMaxSpeed() => classicCarMaxSpeed;
    [HideInInspector] public float GetDriftAngle() => _currentDriftAngle;
    [HideInInspector] public float GetNormalizedDriftAngle() => Mathf.Clamp01(Mathf.Abs(_currentDriftAngle) / maxDriftAngle);
    [HideInInspector] public bool IsDriftBoostActive() => _isDriftBoostActive;
    [HideInInspector] public float GetDriftBoostRemainingTime() => _driftBoostTimer;

    [HideInInspector] public bool ShouldShowDriftEffects() => _isDrifting && Mathf.Abs(_currentDriftAngle) > 10f;
    [HideInInspector] public float GetDriftIntensity() => Mathf.Clamp01(Mathf.Abs(_currentDriftAngle) / maxDriftAngle);
    [HideInInspector] public float GetDriftDirection() => Mathf.Sign(_currentDriftAngle);

    [HideInInspector] public float GetThrottleInput() => _currentThrottleInput;
    [HideInInspector] public float GetSteerInput() => _currentSteerInput;
    [HideInInspector] public float GetCurrentSpeed() => speedKMH;
    [HideInInspector] public bool IsTurboActive() => _isTurboActive;

    [HideInInspector] public bool IsGrounded => isGrounded;

    #endregion

    // ======================================================
    // BODY TILT & VISUAL PHYSICS
    // ======================================================

    #region Body Tilt

    [Header("Body Tilt Settings")]
    [HideInInspector][SerializeField] float maxPitchAngle = 5f;
    [HideInInspector][SerializeField] float maxRollAngle = 10f;
    [HideInInspector][SerializeField] float tiltResponseSpeed = 5f;
    [HideInInspector][SerializeField] float tiltReturnSpeed = 3f;
    [HideInInspector][SerializeField] float brakeTiltMultiplier = 1.5f;
    [HideInInspector][SerializeField] AnimationCurve speedTiltCurve;

    #endregion

    #region visual update state
    [Header("Visual Culling")]
    public bool CanUpdateVisuals { get; private set; } = true;
    #endregion

    // ======================================================
    // GRAVITY / GROUND HUGGING
    // ======================================================

    #region Gravity & Ground Hugging

    [Header("Gravity/Ground Hugging")]
    [HideInInspector][SerializeField] float gravityStrength = 9.81f;
    [HideInInspector][SerializeField] float surfaceAlignmentSpeed = 10f;
    [HideInInspector][SerializeField] float groundHugDistance = 1.5f;

    [HideInInspector] public float extraGripModifier = 1.0f;

    private Vector3 _currentCarUp = Vector3.up;
    private Vector3 currentCarLocalVelocity = Vector3.zero;
    private float carVelocityRatio = 0;

    #endregion

    // ======================================================
    // VISUAL SYSTEM
    // ======================================================

    #region Visual Variables

    [HideInInspector][SerializeField] private float tireRorationSpeed = 3000f;
    [HideInInspector] [SerializeField] private float maxSteerAngle = 30f;

    [Header("Hover Visual Transformation")]
    [HideInInspector][SerializeField] float transitionSpeed = 5f;
    [HideInInspector][SerializeField] Vector3 wheelHoverRotation = new Vector3(0, 180, 90);
    [HideInInspector][SerializeField] Vector3 wheelClassicRotation = new Vector3(0, 180, 0);

    private float _hoverTransitionAlpha = 0f;

    private float _rearWheelRotationAccumulator = 0f;
    private float _frontWheelRotationAccumulator = 0f;
    private Coroutine _transitionCoroutine;
    private Quaternion _currentBaseWheelRot; // Armazena o progresso da transição

    #endregion

    // ======================================================
    // RESPAWN SYSTEM
    // ======================================================

    #region Respawn System

    [Header("Respawn System")]
    [HideInInspector][SerializeField] private float savePositionInterval = 2f;
    [HideInInspector][SerializeField] private float airTimeThreshold = 5f;
    [HideInInspector][SerializeField] private float respawnBoostIntensity = 1.5f;

    private Vector3 _lastSafePosition;
    private Quaternion _lastSafeRotation;
    private float _saveTimer;
    private float _airTimer;
    private bool _isRespawning = false;

    int indexAtSavePoint;

    #endregion

    // ======================================================
    // TURBO SYSTEM
    // ======================================================

    #region Turbo System

    [Header("Turbo & Stamina Settings")]
    [SerializeField] private int NOS_amount = 3;
    [SerializeField] private int max_NOS_amount = 5;
    [SerializeField] private float turboInitialImpulse = 15f;
    [SerializeField] private float turboMaxSpeedMultiplier = 2.0f;
    [SerializeField] private float turboAccelMultiplier = 2.0f;

    [Header("Turbo Cooldown Settings")]
    [SerializeField] private float _turboCooldownTime = 3f;
    [SerializeField] public float turboDuration = 4f;
    [SerializeField] float turboBurstForce = 2.0f;
    [HideInInspector][SerializeField] float turboBodyTiltBase = -5f;

    private float _nextTurboTime = 0f;
    private float turboTimer = 0f;
    private bool _isTurboRequestActive = false;

    [HideInInspector] public float rubberBandingFactor = 1f;

    public int GetNOSAmount()
    {
        return NOS_amount;
    }

    public int GetMaxNOSAmount()
    {
        return max_NOS_amount;
    }

    #endregion

    // ======================================================
    // INTERNAL RUNTIME STATE
    // ======================================================

    #region Internal State
    private float _currentSteerInput;
    private float _currentThrottleInput;
    private bool _currentHandbrakeInput;

    private float _driftExitTime = 0f;

    [SerializeField] private bool _isTurboActive = false;
    private float _turboEndTime = 0f;

    private float _currentSteerAngle;
    public float speedKMH;
    private float _speedRatio;

    // Visual runtime
    private Vector3 _meshLocalEulerAngles;
    private float _currentRoll;
    private float _currentPitch;

    private float _targetPitch = 0f;
    private float _targetRoll = 0f;
    private float _currentBodyPitch = 0f;
    private float _currentBodyRoll = 0f;

    #endregion

    #region collision setup 
    [Header("Controle de Shake de Colisão")]
    private bool enableCollisionShake = true;
    private float shakeIntensity = 0.3f;
    private float minForceToShake = 0.4f;
    private float shakeCooldown = 0.4f;

    private float lastCollisionShakeTime = 0f;
    [Header("Filtro de Direção de Colisão")]
    [Tooltip("Ângulo máximo para considerar colisão frontal/traseira (graus)")]
    [Range(0, 90)]
    float maxFrontalAngle = 60f; // ±45° da frente ou trás

    [Tooltip("Considerar colisões traseiras")]
    bool includeRearCollisions = true;

    [Tooltip("Considerar colisões frontais")]
    bool includeFrontCollisions = true;

    [Header("Spin Out Settings")]
    [SerializeField] float spinMinForce = 20f;
    [SerializeField] float spinMaxForceForCrash = 30f;
    [SerializeField] float spinMinSpeed = 40f;
    [SerializeField] float spinTorqueForce = 18f;
    [SerializeField] float spinDuration = 0.6f;
    [SerializeField][Range(0,90)] float lateralMinAngle = 60f;

    #endregion

    // ======================================================
    // Input Handling
    // ======================================================

    #region Input Handling

    public void SetSteering(float value)
    {
        _currentSteerInput = Mathf.Clamp(value, -1f, 1f);
    }

    public void SetThrottle(float value)
    {
        _currentThrottleInput = Mathf.Clamp(value, -1f, 1f);
    }

    public void SetHandbrake(bool value)
    {
        if (value && !_currentHandbrakeInput && isGrounded)
        {
            TryStartDrift();
        }
        else if (!value && _currentHandbrakeInput && _isDrifting)
        {
            EndDrift(true);
        }

        _currentHandbrakeInput = value;
    }

    #endregion

    float hardAccelCooldown = 0.25f;
    float _nextHardAccelTime;

    float hardBrakeCooldown = 0.25f;
    float _nextHardBrakeTime;

    #region Camera Events

    public event Action OnTurboStart;
    public event Action OnTurboEnd;

    public event Action<float> OnHardAcceleration; // intensidade
    public event Action<float> OnHardBrake;         // intensidade

    public event Action<float> OnDriftStart; // ângulo
    public event Action OnDriftEnd;

    public event Action<float, Vector3> OnCollision; // força, direção
    public event Action<bool> OnCrash;
    public event Action<float> OnJump;                // airtime
    public event Action<float> OnLand;                // airtime

    #endregion

    #region gear variables

    [Header("Arcade Gearbox")]
    [SerializeField] private int totalGears = 5;
    [SerializeField] private float gearShiftCooldown = 0.15f;
    [SerializeField] private float gearAccelerationDrop = 0.85f;
    bool isShifting = false;
    [SerializeField] private float minTimeBetweenShifts = 0.5f; // Tempo mínimo entre trocas
    private float lastShiftTime = -999f;

    [SerializeField] private float[] shiftUpThresholds = {
        0f,     // índice 0 (não usa)
        50f,    // 1ª -> 2ª em 50km/h
        100f,   // 2ª -> 3ª em 100km/h  
        150f,   // 3ª -> 4ª em 150km/h
        200f,   // 4ª -> 5ª em 200km/h
        300f,   // 5ª -> 6ª em 300km/h
        400f    // 6ª -> 7ª em 400km/h
    };

    [SerializeField] private float[] shiftDownThresholds = {
        0f,     // índice 0
        40f,    // 2ª -> 1ª abaixo de 40km/h
        90f,    // 3ª -> 2ª abaixo de 90km/h
        140f,   // 4ª -> 3ª abaixo de 140km/h
        190f,   // 5ª -> 4ª abaixo de 190km/h
        290f,   // 6ª -> 5ª abaixo de 290km/h
        390f    // 7ª -> 6ª abaixo de 390km/h
    };

    [Header("Fake Engine")]

    [SerializeField] private float minRPM = 3000f;
    [SerializeField] private float maxRPM = 9000f;

    [SerializeField] private float upshiftRPM = 8500f;
    [SerializeField] private float downshiftRPM = 3500f;

    [SerializeField] private float engineInertia = 8f;

    public float engineRPM{get; private set;}


    [SerializeField] private int currentGear = 1;
    private float lastGearShiftTime = -999f;

    #endregion

    #region crash

    private SCR_CarCrashPhysics carCrash;
    public bool crashing = false;
    [SerializeField] LayerMask crashable;
    [SerializeField] float crashImpactForce = 35f;
    SCR_MeshDeformer deformer;

    #endregion
    

    // --- Métodos de Física Central ---

    #region Unity Lifecycle
    void Awake()
    {
        _originalDragCoefficient = dragCoefficient;
        _originalMaxSpeed = maxSpeed;

        carCrash = GetComponent<SCR_CarCrashPhysics>();
        deformer = GetComponent<SCR_MeshDeformer>();
    }

    void Start()
    {
        if(rb == null) rb = GetComponent<Rigidbody>();
        
        // Se carBody não foi atribuído, usar o próprio transform
        if(carBody == null)
        {
            Debug.LogWarning("CarBody não atribuído. Usando transform do GameObject.");
            carBody = transform;
        }

        rb.useGravity = false;

        groundSensors = new GroundSensor[rayPoints.Length];

        SwitchToMode(carType);
    }
    
    void Update()
    {
        if(crashing) return;

        UpdateDriftState();
        UpdateDriftBoost();
        CalculateDriftAngle();
        UpdateDragRestoration();
    }

    void FixedUpdate()
    {
        if(!crashing){

            sensorFrameCounter = (sensorFrameCounter + 1) % aiSensorFrequency;
            if (!AIControlled || sensorFrameCounter == 0)
            {
                UpdateGroundSensors();
            }


            if (isGrounded)
            {
                UpdateGravityDirection();
                ApplyAngularDamping();
                ApplyDownforce();
            }

            AlignToTrack();

            ApplySuspension();
            GroundCheck();

            HandleMovement();

            CalculateCarVelocity();

            Visuals();
            
            UpdateBodyTilt();

            if (_isDrifting)
            {
                ApplyDriftForces();
                LimitDriftAngle();
            }

            if (_isTurboActive)
            {
                ApplyTurboPhysics();
            }

            HandleRespawnSystem();

            LimitVelocity();
        }

        UpdateFakeRPM();

        UpdateAutomaticGears();
    }

    #endregion

    #region setup

    public void SetAI(bool isAI, AIRacingController driver)
    {
        AIControlled = isAI;
        if(isAI){
            aiDriver = driver;
        }

        SwitchToMode(carType);
    }

    #endregion

    #region gear change

    private void UpdateAutomaticGears()
    {
        if (!isGrounded || isShifting) return;

        if (Time.time < lastShiftTime + minTimeBetweenShifts) return;

        int targetGear = currentGear;

        // SUBIR MARCHA
        if (currentGear < totalGears && engineRPM >= upshiftRPM)
        {
            targetGear = currentGear + 1;
        }

        // REDUZIR
        else if (currentGear > 1 && engineRPM <= downshiftRPM)
        {
            targetGear = currentGear - 1;
        }

        if (targetGear != currentGear)
        {
            StartCoroutine(GearShiftCoroutine(targetGear));
        }
    }

    private IEnumerator GearShiftCoroutine(int newGear)
    {
        isShifting = true;
        lastShiftTime = Time.time;

        // micro corte de força
        float originalDrop = gearAccelerationDrop;
        gearAccelerationDrop = 0.4f; // quase sem força

        yield return new WaitForSeconds(gearShiftCooldown);

        currentGear = newGear;

        gearAccelerationDrop = originalDrop;
        isShifting = false;
    }

    private void UpdateFakeRPM()
    {
        float forwardSpeed = Mathf.Max(0f, currentCarLocalVelocity.z);

        float effectiveMaxSpeed = maxSpeed;

        if (AIControlled)
            effectiveMaxSpeed *= rubberBandingFactor;

        float gearSpeedRange = effectiveMaxSpeed / totalGears;

        float gearMin = (currentGear - 1) * gearSpeedRange;
        float gearMax = currentGear * gearSpeedRange;

        float gearProgress = Mathf.InverseLerp(gearMin, gearMax, forwardSpeed);

        float effectiveMinRPM = crashing ? 0f : minRPM;

        float targetRPM = Mathf.Lerp(effectiveMinRPM, maxRPM, gearProgress);

        if(crashing) targetRPM = 0f;

        engineRPM = Mathf.Lerp(engineRPM, targetRPM, Time.fixedDeltaTime * engineInertia);

        engineRPM = Mathf.Clamp(engineRPM, effectiveMinRPM, maxRPM);
    }

    #endregion

    #region transition between car modes

    public void ToggleVehicleMode()
    {
        if (!canSwitchType) return;
        SwitchToMode(carType == CarType.classic ? CarType.hover : CarType.classic);
    }

    public void SwitchToMode(CarType newMode)
    {
        carType = newMode;
        isHover = (carType == CarType.hover);

        dragCoefficient = isHover ? hoverCarDragCoefficient : classicCarDragCoefficient;
        acceleration    = isHover ? hoverCarAcceleration    : classicCarAcceleration;
        maxSpeed        = isHover ? hoverCarMaxSpeed         : classicCarMaxSpeed;

        turningCurve = isHover ? hoverCarTurningCurve : classicCarTurningCurve;

        currentAirDrag = isHover ? hoverAirDrag : classicAirDrag;

        currentTargetSuspensionLength = isHover ? hoverDistance : restLenght;
        currentDamper = isHover ? hoverDamper : classicDamper;


        if (isHover)
            rb.AddForce(transform.up * 5f, ForceMode.VelocityChange);

        if (_transitionCoroutine != null) StopCoroutine(_transitionCoroutine);
        _transitionCoroutine = StartCoroutine(AnimateWheelTransition());
    }


    #endregion

    #region Movement

    private void HandleMovement()
    {
        bool hoverLostSurface = isHover && cachedGroundedCount == 0;
        bool classicOffGround = !isHover && !isGrounded;

        if(!hoverLostSurface && !classicOffGround)
        {
            if(_currentThrottleInput > 0.1f)
            {
                Acceleration();
            }
            if(_currentThrottleInput < -0.1f)
            {
                Deaceleration();
            }

            SidewaysDrag();

            if(carType == CarType.hover)
            {
                ApplyAirDrag();
            }
        }
        else
        {
            AirControl();
        }

        Turn();

    }

    private void LimitVelocity()
    {
        // Pegamos a velocidade no espaço local
        Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);

        // Definimos o limite máximo (considerando o turbo se ele estiver ativo)
        float maxLimit = _isTurboActive ? maxSpeed * turboMaxSpeedMultiplier : maxSpeed;
        maxLimit *= rubberBandingFactor;
        float minLimit = -(maxSpeed / 3f);

        // Aplica o Clamp apenas no eixo Z (frente/trás)
        localVel.z = Mathf.Clamp(localVel.z, minLimit, maxLimit);

        // Devolve a velocidade para o Rigidbody mantendo os eixos X e Y (pulo e drift)
        rb.linearVelocity = transform.TransformDirection(localVel);
    }

    private void Acceleration()
    {
        if(isShifting)return;

        float effectiveMaxSpeed = maxSpeed;

        if (AIControlled) effectiveMaxSpeed *= rubberBandingFactor;
        
        if (_isTurboActive) effectiveMaxSpeed *= turboMaxSpeedMultiplier;

        if (Mathf.Abs(currentCarLocalVelocity.z) >= effectiveMaxSpeed) return;

        float currentAcceleration = acceleration;

        float gearFactor = Mathf.Lerp(
            1.25f,
            0.8f,
            (float)(currentGear - 1) / (totalGears - 1)
        );

        currentAcceleration *= gearFactor * gearAccelerationDrop;

        
        // Multiplicador de aceleração durante drift
        if (_isDrifting) currentAcceleration *= driftAccelerationMultiplier;

        if (_isTurboActive) currentAcceleration *= turboAccelMultiplier;
        
        // Multiplicador de aceleração durante boost
        if (_isDriftBoostActive) currentAcceleration *= 2f; // Dobra a aceleração durante o boost

        if (AIControlled)  currentAcceleration *= rubberBandingFactor;
        
        rb.AddForceAtPosition(currentAcceleration * _currentThrottleInput * transform.forward, accelerationPoint.position, ForceMode.Acceleration);

        if (Time.time > _nextHardAccelTime &&
                _currentThrottleInput > 0.9f &&
                currentCarLocalVelocity.z < maxSpeed * 0.5f)
        {
            OnHardAcceleration?.Invoke(currentCarLocalVelocity.z / maxSpeed);
            _nextHardAccelTime = Time.time + hardAccelCooldown;
        }
    }

    private void Deaceleration()
    {
        if(isShifting)return;

        float brakeMultiplier = 1f;

        if(currentCarLocalVelocity.z > 1f && !AIControlled) brakeMultiplier = 1.1f;

        if(currentCarLocalVelocity.z < 0.1f)
        {
            if (Mathf.Abs(currentCarLocalVelocity.z) >= maxSpeed / 3) return;
        }

        float currentDeceleration = deceleration;

        if (AIControlled)   currentDeceleration *= rubberBandingFactor;

        rb.AddForceAtPosition(currentDeceleration * brakeMultiplier * _currentThrottleInput * transform.forward, accelerationPoint.position, ForceMode.Acceleration);
   
        if (Time.time > _nextHardBrakeTime &&
            _currentThrottleInput < -0.9f &&
            currentCarLocalVelocity.z > 10f)
        {
            OnHardBrake?.Invoke(currentCarLocalVelocity.z / maxSpeed);
            _nextHardBrakeTime = Time.time + hardBrakeCooldown;
        }
    }

    private void Turn()
    {
        float TurnPowerMultiplier = (isGrounded || isHover) ? 1 : 0.5f;

        float newSteerStrenght = (carType == CarType.classic && !_isDrifting) ? steerStrenght / 50f : steerStrenght;
        float steerPower = newSteerStrenght * TurnPowerMultiplier;
        float steerInput = _currentSteerInput;

        // --- LÓGICA DE SELF-STEERING (DENTRO DO DRIFT) ---
        if (_isDrifting)
        {
            // Se o jogador NÃO está dando input de direção (analogico solto ou neutro)
            if (Mathf.Abs(steerInput) < 0.1f)
            {
                // Calcula um torque suave oposto ao ângulo do drift para alinhar o nariz com a velocidade
                // Se o drift angle é positivo (cauda para a esquerda), precisamos de torque negativo para alinhar
                float selfSteerFactor = -(_currentDriftAngle / maxDriftAngle);

                float TimeMultiplier = carType == CarType.classic ? 1f : Time.fixedDeltaTime;
                steerInput = selfSteerFactor * driftSelfSteerStrength * TimeMultiplier;
                
                // Limitamos para não ser mais forte que um input manual
                steerInput = Mathf.Clamp(steerInput, -driftCounterSteerLimit, driftCounterSteerLimit);
            }
            // Se estiver virando contra a direção do drift (Counter-Steering manual)
            else if (Mathf.Sign(steerInput) != Mathf.Sign(_currentDriftAngle))
            {
                steerPower *= 1.25f; // Mantemos sua lógica de recuperação mais rápida
            }
        }

        // Fator que aumenta o poder de direção quando acelerando
        float accelerationSteerBoost = 1f + (_currentThrottleInput * 0.3f);
        
        // Usa velocidade absoluta para a curva
        float speedFactor = turningCurve.Evaluate(Mathf.Abs(carVelocityRatio));

        if (Mathf.Abs(steerInput) < 0.1f && isGrounded && !_isDrifting)
        {
            // Reduz drasticamente a rotação quando solta o volante (efeito de centralização)
            Vector3 localAngularVel = transform.InverseTransformDirection(rb.angularVelocity);
            localAngularVel.y *= 0.9f; // Amortece o giro no eixo Y
            rb.angularVelocity = transform.TransformDirection(localAngularVel);
        }

        Vector3 turnAxis = cachedSurfaceNormal;
        
        if(carType == CarType.classic && !_isDrifting)
        {
            // Aplica o torque final (usando o steerInput processado pelo self-steering se necessário)
            rb.AddTorque(turnAxis * steerPower * steerInput * speedFactor * accelerationSteerBoost, ForceMode.VelocityChange);
        }
        else
        {
            rb.AddTorque(turnAxis * steerPower * steerInput * speedFactor * accelerationSteerBoost, ForceMode.Acceleration);
        }
    }

    private void SidewaysDrag()
    {
        // 1. Projetar velocidade no plano da superfície
        Vector3 planarVelocity = Vector3.ProjectOnPlane(rb.linearVelocity, cachedSurfaceNormal);

        // 2. Direção lateral real no plano
        Vector3 lateralDir = Vector3.Cross(cachedSurfaceNormal, transform.forward).normalized;

        // 3. Velocidade lateral real
        float lateralSpeed = Vector3.Dot(planarVelocity, lateralDir);

        // 4. Drag lateral base
        float dragForceAmount = -lateralSpeed * dragCoefficient;
        Vector3 dragForce = lateralDir * dragForceAmount;

        rb.AddForce(dragForce, ForceMode.Acceleration);

        // 5. Grip assistido (Classic)
        if (carType == CarType.classic && isGrounded && Mathf.Abs(_currentSteerInput) > 0.1f)
        {
            float speedFactor = planarVelocity.magnitude / maxSpeed;
            Vector3 gripForce = lateralDir * _currentSteerInput * speedFactor;

            rb.AddForce(gripForce, ForceMode.Acceleration);
        }
    }

    #endregion

    #region Suspension

    void ApplySuspension()
    {
        float targetDistance = currentTargetSuspensionLength;
        float damperStiffness = currentDamper;


        float maxVisualLenght = .125f + .15f;

        for (int i = 0; i < rayPoints.Length; i++)
        {
            if (!groundSensors[i].hit)
            {
                wheelsGrounded[i] = 0;

                Vector3 airPos = isHover
                    ? rayPoints[i].position
                    : rayPoints[i].position - transform.up * maxVisualLenght;

                SetTirePosition(tires[i], airPos);
                continue;
            }

            wheelsGrounded[i] = 1;
            RaycastHit hit = groundSensors[i].hitInfo;

            float currentSpringLength = hit.distance - wheelRadius;
            float springCompressionRatio = (targetDistance - currentSpringLength) / springTravel;

            float springVelocity = Vector3.Dot(
                rb.GetPointVelocity(rayPoints[i].position),
                transform.up
            );

            float springForce = springStiffness * springCompressionRatio;
            float dampForce = damperStiffness * springVelocity;
            float netForce = springForce - dampForce;

            if (!isHover)
            {
                if (currentSpringLength > maxVisualLenght)
                {
                    float distanceGap = currentSpringLength - maxVisualLenght;
                   // float suctionMultiplier = 50f;

                    netForce -= distanceGap * rb.mass;// * suctionMultiplier;
                }
                else if (currentSpringLength < targetDistance * 0.5f)
                {
                    netForce *= 1.5f;
                }
            }

            rb.AddForceAtPosition(netForce * transform.up, rayPoints[i].position);

            // -----------------------------
            // VISUAL
            // -----------------------------
            if (isHover)
            {
                SetTirePosition(tires[i], rayPoints[i].position);
            }
            else
            {
                float visualSpringDistance = Mathf.Min(currentSpringLength, maxVisualLenght);
                Vector3 visualPos = rayPoints[i].position - transform.up * visualSpringDistance;
                SetTirePosition(tires[i], visualPos);
            }
        }

        // Gravidade baseada na normal cacheada
        rb.AddForce(-_currentCarUp * rb.mass * gravityStrength);
    }


    #endregion

    #region Visuals

    public void SetVisualState(bool canUpdate)
    {
        if (CanUpdateVisuals == canUpdate) return;
        CanUpdateVisuals = canUpdate;
    }


    private void Visuals()
    {
        if(!CanUpdateVisuals)return;
        TireVisuals();
    }

    private void TireVisuals()
    {
        // Se estiver no modo Hover, as rodas ficam estáticas na posição da transição
        if (isHover) return;

        float steeringAngle = maxSteerAngle * _currentSteerInput;
        
        // Acumuladores de rotação (X local)
        // Traseira: PURA tração (só com acelerador)
        if(Mathf.Abs(_currentThrottleInput ) > 0.1f){
            _rearWheelRotationAccumulator += tireRorationSpeed * _currentThrottleInput * Time.deltaTime;
        }
        else
        {
            _rearWheelRotationAccumulator += tireRorationSpeed * carVelocityRatio * Time.deltaTime;
        }
        // Dianteira: Pura velocidade (rolagem baseada no movimento)
        _frontWheelRotationAccumulator += tireRorationSpeed * carVelocityRatio * Time.deltaTime;

        for (int i = 0; i < tires.Length; i++)
        {
            if (tires[i] == null) continue;

            if (i >= 2) // RODAS TRASEIRAS
            {
                // Combinamos a inclinação da transição com o giro do acumulador
                tires[i].transform.localRotation = _currentBaseWheelRot * Quaternion.Euler(_rearWheelRotationAccumulator, 0, 0);
            }
            else // RODAS DIANTEIRAS
            {
                // Rotação do pneu (X)
                tires[i].transform.localRotation = _currentBaseWheelRot * Quaternion.Euler(_frontWheelRotationAccumulator, 0, 0);

                // Esterço no Parent (Y)
                if (frontTiresParent[i] != null)
                {
                    // Multiplicamos a rotação base da transição pelo ângulo de esterço
                    // Isso permite que a roda esterce mesmo se estiver "meio inclinada" durante a transição
                    frontTiresParent[i].transform.localRotation = _currentBaseWheelRot * Quaternion.Euler(0, steeringAngle, 0);
                }
            }
        }
    }

    private IEnumerator AnimateWheelTransition()
    {
        float targetAlpha = (carType == CarType.hover) ? 1f : 0f;
        
        // Definimos os Quaternions fixos de início e fim para o Slerp não se perder
        Quaternion startRot = Quaternion.Euler(wheelClassicRotation);
        Quaternion endRot = Quaternion.Euler(wheelHoverRotation);

        _currentBaseWheelRot = startRot;

        while (!Mathf.Approximately(_hoverTransitionAlpha, targetAlpha))
        {
            _hoverTransitionAlpha = Mathf.MoveTowards(_hoverTransitionAlpha, targetAlpha, Time.deltaTime * transitionSpeed);
            
            // Slerp é muito mais suave para rotações mecânicas
            _currentBaseWheelRot = Quaternion.Slerp(startRot, endRot, _hoverTransitionAlpha);
            
            ApplyBaseTransformations();
            
            yield return null;
        }
        
        // Garante que o valor final seja exato ao terminar
        _currentBaseWheelRot = (carType == CarType.hover) ? endRot : startRot;
        ApplyBaseTransformations();
        
        _transitionCoroutine = null;
    }

    private void ApplyBaseTransformations()
    {
        for (int i = 0; i < tires.Length; i++)
        {
            if (tires[i] == null) continue;
            
            if (i < 2 && frontTiresParent[i] != null)
                tires[i].transform.localRotation = _currentBaseWheelRot;
            else
                tires[i].transform.localRotation = _currentBaseWheelRot;
        }
    }
    
    private void UpdateBodyTilt()
    {
        if (carBody == null || !CanUpdateVisuals) return;

        // Calcular velocidade atual em magnitude
        float currentSpeed = rb.linearVelocity.magnitude;
        float speedFactor = speedTiltCurve.Evaluate(Mathf.Clamp01(currentSpeed / maxSpeed));

        if (_isTurboActive)
        {
            float turboIntensity = 1.5f; // Multiplicador de agressividade do tilt no turbo
            _targetPitch = -maxPitchAngle * turboIntensity;

            // No Turbo, o Roll (inclinação lateral) é reduzido para passar sensação de estabilidade em alta velocidade
            _targetRoll = (-maxRollAngle * _currentSteerInput * speedFactor) * 0.3f;

            _targetPitch += UnityEngine.Random.Range(-0.5f, 0.5f);
        }
        else
        {

            float driftRollMultiplier = _isDrifting ? 2f : 1f;
            
            // Calcular tilt para frente/trás (pitch) baseado na aceleração/freio
            if (_currentThrottleInput > 0.1f)
            {
                // Acelerando - tilt para trás
                _targetPitch = -maxPitchAngle * _currentThrottleInput * speedFactor;
            }
            else if (_currentThrottleInput < -0.1f && rb.linearVelocity.z > 1f)
            {
                // Freando - tilt para frente (mais pronunciado)
                _targetPitch = maxPitchAngle * _currentThrottleInput * brakeTiltMultiplier * speedFactor;
            }
            else
            {
                // Sem input - retornar ao normal
                _targetPitch = 0f;
            }
            
            // Calcular tilt para os lados (roll) baseado na direção
            if (Mathf.Abs(_currentSteerInput) > 0.1f && currentSpeed > 1f)
            {
                // Virando - tilt para o lado oposto da curva (contra-steer visual)
                _targetRoll = -maxRollAngle * _currentSteerInput * speedFactor * driftRollMultiplier;
            }
            else
            {
                // Sem direção ou velocidade baixa - retornar ao normal
                _targetRoll = 0f;
            }
        }
        // Aplicar suavização (lerp) para transições suaves
        float currentResponseSpeed = (Mathf.Abs(_targetPitch) > Mathf.Abs(_currentBodyPitch) || 
                                     Mathf.Abs(_targetRoll) > Mathf.Abs(_currentBodyRoll)) ? 
                                     tiltResponseSpeed : tiltReturnSpeed;
        
        _currentBodyPitch = Mathf.Lerp(_currentBodyPitch, _targetPitch, Time.fixedDeltaTime * currentResponseSpeed);
        _currentBodyRoll = Mathf.Lerp(_currentBodyRoll, _targetRoll, Time.fixedDeltaTime * currentResponseSpeed);
        
        // Aplicar a rotação ao corpo do carro
        // Preservar a rotação Y (direção) original do corpo
        float currentYaw = carBody.localEulerAngles.y;
        carBody.localEulerAngles = new Vector3(_currentBodyPitch, currentYaw, _currentBodyRoll);
    }

    private void SetTirePosition(GameObject tire, Vector3 targetPosition)
    {
        if(!CanUpdateVisuals) return;
        tire.transform.position = targetPosition;
    }
    #endregion

    #region Physics - Stability

    public void ApplyDownforce()
    {
        float speed = rb.linearVelocity.magnitude;

        if (!isHover)
        {
            // Modo Classic: Física quadrática para sensação de Stock Car pesado
            float df = (speed * speed) * downforceAmount;
            
            // Clamp de segurança para evitar que o carro atravesse o chão em velocidades extremas
            float maxForce = rb.mass * Mathf.Abs(Physics.gravity.y) * 4f;
            df = Mathf.Clamp(df, 0f, maxForce);

            rb.AddForce(-_currentCarUp * df, ForceMode.Force);
            return;
        }

        // --- Daqui para baixo, a lógica original do Hover foi mantida intacta ---
        if (cachedGroundedCount == 0) return;

        float heightRatio = cachedAverageHeight / restLenght;
        if (heightRatio < minHeightThreshold) return;

        float speedFactor = useDynamicDownforce
            ? speed / (maxSpeed / 3.6f)
            : 1f;

        rb.AddForce(-_currentCarUp * hoverDownforceAmount * speedFactor, ForceMode.Force);
    }


    private void ApplyAirDrag()
    {
        float forwardSpeed = currentCarLocalVelocity.z;

        float speedRatio = Mathf.Clamp01(forwardSpeed / maxSpeed);

        // Só começa a agir depois de 70% da velocidade máxima
        if (speedRatio < 0.7f) return;

        float dragStrength = Mathf.Lerp(
            0f,
            currentAirDrag,
            (speedRatio - 0.7f) / 0.3f
        );

        Vector3 dragForce = -transform.forward * forwardSpeed * forwardSpeed * dragStrength;

        rb.AddForce(dragForce, ForceMode.Acceleration);
    }


    private void ApplyAngularDamping()
    {
        // Amortecimento de Roll (Eixo Z Local) e Pitch (Eixo X Local)
        // O Yaw (Eixo Y Local) é tratado pelo Turn e SidewaysDrag, deve ser menos amortecido.
        
        // Converte a velocidade angular global para o espaço local do carro
        Vector3 localAngularVelocity = transform.InverseTransformDirection(rb.angularVelocity);
        
        // Amortecer os eixos X (Pitch) e Z (Roll)
        // Angular Damping Strength (Ajuste este valor)
        float dampingStrength = isGrounded ? 50f : 10f; // Mais amortecimento no chão
        
        // Durante drift, reduzimos o amortecimento para permitir mais rotação
        if (_isDrifting)
        {
            dampingStrength *= 0.3f;
        }
        
        // Força de Amortecimento = -Velocidade Angular Local * Força de Amortecimento
        localAngularVelocity.x *= -dampingStrength; // Amortecer Pitch
        localAngularVelocity.z *= -dampingStrength; // Amortecer Roll
        
        // Converte de volta para o espaço global e aplica como Torque
        Vector3 dampingTorque = transform.TransformDirection(localAngularVelocity);
        
        rb.AddTorque(dampingTorque, ForceMode.Acceleration);
    }

    private void AirControl()
    {
        if (isGrounded) return; // Só aplica se não estiver no chão

        // 1. Controle Manual de Atitude (Pitch e Roll)
        // O input de aceleração/freio (Pitch) e o input de direção (Roll)
        
        // Torque X (Pitch): Controle de nariz para cima/baixo
        float pitchInput = _currentThrottleInput; 
        
        // Torque Z (Roll): Controle de inclinação lateral
        float rollInput = _currentSteerInput; 

        // O Torque aplicado deve ser FORTE para o feeling arcade (airControlStrength deve ser alto, ex: 100-200)
        Vector3 controlTorque = new Vector3(
            pitchInput, 
            0, // Deixamos o Yaw (Y) de lado no ar, ou aplicamos um pouco para virar o nariz
            rollInput
        ) * airControlStrength;
        
        // Aplica o torque no espaço local do carro
        rb.AddRelativeTorque(controlTorque, ForceMode.Acceleration);

        // 2. Força de Nivelamento (Auto-Leveling)
        ForceLeveling();
        
        rb.AddForce(-transform.up * 50f, ForceMode.Acceleration);
    }

    private void ForceLeveling()
    {
        // Apenas nivelar se não houver input de controle aéreo substancial
        if (Mathf.Abs(_currentThrottleInput) > 0.1f || 
            Mathf.Abs(_currentSteerInput) > 0.1f) 
            return;

        // A força de correção deve ser menor do que a força de controle (airControlStrength)
        float correctionStrength = airControlStrength * 0.1f; 

        // 1. Correção do Pitch (Eixo X)
        // Alinha o vetor 'up' do carro com o 'up' do mundo
        Vector3 torqueAxisX = Vector3.Cross(transform.up, Vector3.up);
        float angleToLevelX = Vector3.SignedAngle(transform.up, Vector3.up, transform.right);

        // 2. Correção do Roll (Eixo Z)
        Vector3 torqueAxisZ = Vector3.Cross(transform.up, Vector3.up);
        float angleToLevelZ = Vector3.SignedAngle(transform.up, Vector3.up, transform.forward);

        // Combinar Pitch e Roll Correction
        // Nota: É mais fácil e mais comum usar quaternions para nivelamento
        Quaternion targetRotation = Quaternion.LookRotation(transform.forward, Vector3.up);
        Quaternion currentRotation = transform.rotation;
        
        Quaternion deltaRotation = targetRotation * Quaternion.Inverse(currentRotation);
        
        // Converte a diferença de rotação para um vetor angular (eixos X, Y, Z)
        deltaRotation.ToAngleAxis(out float angle, out Vector3 axis);
        
        if (angle > 180f) angle -= 360f;
        if (angle > 0.01f)
        {
            // Aplica torque corretivo suave, ignorando a correção Y (Yaw) para não interferir na direção de movimento
            Vector3 correctionTorque = axis * angle * correctionStrength;
            correctionTorque.y = 0; // Não corrija o Yaw

            rb.AddTorque(correctionTorque, ForceMode.Acceleration);
        }
    }

    private void UpdateGravityDirection()
    {
        _currentCarUp = cachedSurfaceNormal;
    }


    void AlignToTrack()
    {
        Quaternion targetRotation =
            Quaternion.FromToRotation(transform.up, cachedSurfaceNormal) * transform.rotation;

        rb.MoveRotation(
            Quaternion.Slerp(
                rb.rotation,
                targetRotation,
                Time.fixedDeltaTime * surfaceAlignmentSpeed
            )
        );
    }


    #endregion

    #region Physics - Drift System

    private void TryStartDrift()
    {
        // Só pode começar drift se estiver no chão
        if ((!isHover && !isGrounded)) return;
        
        // Calcular velocidade mínima para entrar em drift
        float minDriftSpeed = maxSpeed * driftEnterThreshold;
        
        // Verificar se está rápido o suficiente para drift
        if (rb.linearVelocity.magnitude < minDriftSpeed)
            return;
            
        // Só começa drift se não estiver já em drift
        if (!_isDrifting)
        {
            StartDrift();
        }
    }

    private void StartDrift()
    {
        _isDrifting = true;
        
        // Ajustar configurações para drift
        dragCoefficient = _originalDragCoefficient * driftStability;

        //CalculateDriftAngle();

        OnDriftStart?.Invoke(Mathf.Abs(15f));
    }

    private void UpdateDriftState()
    {
        if (!_isDrifting) return;
        
        // Calcular velocidade mínima de segurança para não driftar parado
        float minDriftSpeed = maxSpeed * driftEnterThreshold * 0.3f;
        
        // O drift agora SÓ termina automaticamente se:
        // 1. O carro sair do chão
        // 2. A velocidade ficar muito baixa
        if ((!isHover && !isGrounded) || rb.linearVelocity.magnitude < minDriftSpeed)
        {
            EndDrift(false); // Sai sem boost pois perdeu o controle/velocidade
        }
    }

    private void CalculateDriftAngle()
    {
        // Calcular o ângulo entre a direção do carro e a direção da velocidade
        Vector3 carForward = transform.forward;
        Vector3 velocityDirection = rb.linearVelocity.normalized;
        
        // Ignorar componente vertical
        carForward.y = 0;
        velocityDirection.y = 0;
        
        // Normalizar após remover componente Y
        carForward.Normalize();
        velocityDirection.Normalize();
        
        // Verificar se temos vetores válidos
        if (carForward.magnitude > 0.1f && velocityDirection.magnitude > 0.1f)
        {
            float rawAngle = Vector3.Angle(carForward, velocityDirection);
            
            // Determinar direção do drift (positivo = drift para direita, negativo = para esquerda)
            float driftDirection = Mathf.Sign(Vector3.Cross(carForward, velocityDirection).y);
            
            // Usar ângulo absoluto para cálculos internos
            _currentDriftAngle = rawAngle * driftDirection;
        }
        else
        {
            // Resetar ângulo se não houver velocidade suficiente
            _currentDriftAngle = 0f;
        }
    }

    private void LimitDriftAngle()
    {
        // Limitar o ângulo máximo de drift
        if (Mathf.Abs(_currentDriftAngle) > maxDriftAngle)
        {
            // Aplicar força de correção para reduzir o drift
            float correctionStrength = 0.5f;
            float correctionTorque = -Mathf.Sign(_currentDriftAngle) * correctionStrength;
            rb.AddRelativeTorque(0, correctionTorque, 0, ForceMode.Acceleration);
        }
    }

   private void ApplyDriftForces()
    {
        if ((!isHover && !isGrounded) || !_isDrifting) return;

        // 1. Calcular intensidade do drift (0 a 1)
        float driftIntensity = Mathf.Clamp01(Mathf.Abs(_currentDriftAngle) / maxDriftAngle);
        
        // 2. FORÇA DE EMPUXO FRONTAL
        // Em vez de apenas manter a velocidade, vamos dar um pequeno boost constante
        // para que o carro sinta que está "tracionando" mesmo de lado.
        float forwardSpeed = currentCarLocalVelocity.z;
        if (forwardSpeed > 5f) // Só aplica se o carro já estiver em movimento
        {
            // Multiplicamos por 1.2f para compensar o arrasto lateral natural da V1
            float speedMaintainForce = driftIntensity * acceleration * speedMaintainForceMultiplier * 1.2f;
            rb.AddForce(transform.forward * speedMaintainForce, ForceMode.Acceleration);
        }

        // 3. ESTABILIDADE DE ÂNGULO (Drift Lock-in)
        // Se o jogador não está dando input, nós "congelamos" a rotação.
        // Isso evita que o carro rode sozinho (spin out) e deixa o drift "no trilho".
        if (Mathf.Abs(_currentSteerInput) < 0.1f)
        {
            Vector3 localAV = transform.InverseTransformDirection(rb.angularVelocity);
            
            // Suavizamos a velocidade angular para zero. 10f é o "rigidez" do travamento.
            localAV.y = Mathf.Lerp(localAV.y, 0f, Time.fixedDeltaTime * 10f);
            
            rb.angularVelocity = transform.TransformDirection(localAV);
        }

        // 4. FORÇA CENTRÍFUGA
        float centrifugalForce = rb.linearVelocity.magnitude * driftIntensity * 0.3f;
        Vector3 forceDirection = -transform.right * Mathf.Sign(_currentDriftAngle);
        rb.AddForce(forceDirection * centrifugalForce, ForceMode.Acceleration);
        
        // 5. LIMITADOR DE VELOCIDADE ANGULAR
        Vector3 av = transform.InverseTransformDirection(rb.angularVelocity);
        float maxRotation = 2.5f; // Ajuste este valor para mais ou menos agilidade
        av.y = Mathf.Clamp(av.y, -maxRotation, maxRotation);
        rb.angularVelocity = transform.TransformDirection(av);
    }

    private void EndDrift(bool giveBoost)
    {
        _isDrifting = false;

        OnDriftEnd?.Invoke();
        
        _isRestoringDrag = true;
        _dragRestoreTimer = 0f;
        
        // Aplicar boost se merecido
        if (giveBoost && Mathf.Abs(_currentDriftAngle) > 20f) // Ângulo mínimo para ganhar boost
        {
            ApplyDriftBoost();
        }
    }

    private void UpdateDragRestoration()
    {
        if (!_isRestoringDrag) return;

        _dragRestoreTimer += Time.deltaTime;
        float lerpPercent = _dragRestoreTimer / dragRestoreDuration;

        // Interpola entre o drag de drift e o original
        float driftDrag = _originalDragCoefficient * driftStability;
        dragCoefficient = Mathf.Lerp(driftDrag, _originalDragCoefficient, lerpPercent);

        if (lerpPercent >= 1f)
        {
            dragCoefficient = _originalDragCoefficient;
            _isRestoringDrag = false;
        }
    }

    private void ApplyDriftBoost()
    {
        _isDriftBoostActive = true;
        _driftBoostTimer = driftBoostDuration;
        
        // 1. Direção do Nariz
        Vector3 carForward = transform.forward;
        
        // 2. Direção do Movimento Atual (Velocidade)
        Vector3 velocityDir = rb.linearVelocity.normalized;
        
        // 3. Mistura (Lerp): 70% frente do carro, 30% direção da velocidade
        // Isso "puxa" o carro para a frente, mas respeita a inércia atual
        Vector3 boostDirection = Vector3.Lerp(velocityDir, carForward, 0.7f).normalized;
        
        // Aplicar a força na direção misturada
        Vector3 boostForce = boostDirection * driftBoostForce;
        rb.AddForce(boostForce, ForceMode.VelocityChange);
        
        // Aumentar velocidade máxima temporariamente
        maxSpeed = maxDriftBoostSpeed;
    }

    private void UpdateDriftBoost()
    {
        if (!_isDriftBoostActive) return;
        
        _driftBoostTimer -= Time.deltaTime;
        
        if (_driftBoostTimer <= 0) EndDriftBoost();
    }

    private void EndDriftBoost()
    {
        _isDriftBoostActive = false;
        maxSpeed = _originalMaxSpeed;
    }

    #endregion

    #region collision

    void OnCollisionEnter(Collision collision)
    {
        if (collision.transform.root == transform.root || collision.gameObject == this.gameObject)
            return;

        if (((1 << collision.gameObject.layer) & drivable) != 0)
            return;

        float impactForce = collision.relativeVelocity.magnitude;
        float currentSpeed = rb.linearVelocity.magnitude;

        Vector3 dir = -collision.contacts[0].normal;

        bool isCrashableObject = ((1 << collision.gameObject.layer) & crashable) != 0;

        //  CRASH
        if (isCrashableObject &&
            impactForce > crashImpactForce &&
            IsFrontalCollision(dir) && !_isTurboActive)
        {
            if (crashing) return;

            OnCrash?.Invoke(true);
            deformer.Deform(collision.contacts[0].point, collision.relativeVelocity);
            carCrash.TriggerCrash();
            Invoke("ResetCrashCam", carCrash.crashDuration);
            return;
        }

        // SPIN OUT
        if (impactForce > spinMinForce &&
            impactForce < spinMaxForceForCrash &&
            currentSpeed > spinMinSpeed &&
            IsLateralCollision(dir) && !_isTurboActive)
        {
            TriggerSpin(dir, impactForce);
            return;
        }

        // NEUTRA (SHAKE)
        if (enableCollisionShake &&
            impactForce >= minForceToShake &&
            Time.time >= lastCollisionShakeTime + shakeCooldown)
        {
            if (IsFrontalOrRearCollision(dir))
            {
                float finalForce = Mathf.Clamp01(impactForce / 20f) * shakeIntensity;
                OnCollision?.Invoke(finalForce, dir);
                lastCollisionShakeTime = Time.time;
            }
        }
    }

    private bool IsFrontalOrRearCollision(Vector3 collisionDirection)
    {
        Vector3 planarForward = Vector3.ProjectOnPlane(transform.forward, cachedSurfaceNormal);
        Vector3 planarCollision = Vector3.ProjectOnPlane(collisionDirection, cachedSurfaceNormal);

        if (planarCollision.sqrMagnitude < 0.0001f)
            return false;

        planarForward.Normalize();
        planarCollision.Normalize();

        float dot = Vector3.Dot(planarForward, planarCollision);
        float minDot = Mathf.Cos(maxFrontalAngle * Mathf.Deg2Rad);

        bool isFrontal = includeFrontCollisions && dot >= minDot;
        bool isRear    = includeRearCollisions && dot <= -minDot;

        return isFrontal || isRear;
    }

    private bool IsFrontalCollision(Vector3 collisionDirection)
    {
        Vector3 planarForward = Vector3.ProjectOnPlane(transform.forward, cachedSurfaceNormal);
        Vector3 planarCollision = Vector3.ProjectOnPlane(collisionDirection, cachedSurfaceNormal);

        if (planarCollision.sqrMagnitude < 0.0001f)
            return false;

        planarForward.Normalize();
        planarCollision.Normalize();

        float dot = Vector3.Dot(planarForward, planarCollision);
        float minDot = Mathf.Cos(maxFrontalAngle * Mathf.Deg2Rad);

        return includeFrontCollisions && dot >= minDot;
    }

    void TriggerSpin(Vector3 collisionDirection, float force)
    {
        if (crashing) return;
        if (_isDrifting) return; // não spin durante drift

        float spinDirection = Mathf.Sign(Vector3.Dot(transform.right, collisionDirection));

        rb.angularVelocity = Vector3.zero;

        rb.AddTorque(
            transform.up * spinDirection * spinTorqueForce * (force / spinMinForce),
            ForceMode.VelocityChange
        );

        rb.AddForce(-transform.forward * 5f, ForceMode.VelocityChange);

        OnCollision?.Invoke(0.5f, collisionDirection);
    }

    bool IsLateralCollision(Vector3 collisionDirection)
    {
        Vector3 planarForward = Vector3.ProjectOnPlane(transform.forward, cachedSurfaceNormal);
        Vector3 planarCollision = Vector3.ProjectOnPlane(collisionDirection, cachedSurfaceNormal);

        if (planarCollision.sqrMagnitude < 0.0001f)
            return false;

        planarForward.Normalize();
        planarCollision.Normalize();

        float dot = Vector3.Dot(planarForward, planarCollision);

        float minDot = Mathf.Cos(lateralMinAngle * Mathf.Deg2Rad);

        // lateral = região entre frontal e traseira
        return Mathf.Abs(dot) < minDot;
    }

    void ResetCrashCam()
    {
        OnCrash?.Invoke(false);
    }

    #endregion

    #region respawn 

    private void HandleRespawnSystem()
    {
        if (!isGrounded)
        {
            _airTimer += Time.fixedDeltaTime;

            // Se passar do tempo limite no ar, executa o respawn
            if (_airTimer >= airTimeThreshold && !_isRespawning) ExecuteRespawn();
        }
    }

    bool IsAreaSafe()
    {
        // Precisa ter chão suficiente
        if (cachedGroundedCount < 3)
            return false;

        float upDot = Vector3.Dot(cachedSurfaceNormal, Vector3.up);
        if (upDot < 0.7f) 
            return false;

        return true;
    }

    private void ExecuteRespawn()
    {
        _isRespawning = true;
        
        // Zera as forças físicas para não respawnar com a inércia da queda
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Reposiciona o carro
        transform.position = _lastSafePosition;
        Transform target = GetRespawnTarget();

        if (target != null)
        {
            Vector3 direction = (target.position - _lastSafePosition).normalized;

            // Projetar direção no plano da pista
            Vector3 planarDir = Vector3.ProjectOnPlane(direction, cachedSurfaceNormal).normalized;

            if (planarDir.sqrMagnitude > 0.01f)
            {
                Quaternion newRotation = Quaternion.LookRotation(planarDir, cachedSurfaceNormal);
                transform.rotation = newRotation;
            }
            else
            {
                transform.rotation = _lastSafeRotation;
            }
        }
        else
        {
            transform.rotation = _lastSafeRotation;
        }

        // Aplica um boost de aceleração imediato para retomar a corrida
        if(!AIControlled)
        {
            //rb.AddForce(transform.forward * acceleration * respawnBoostIntensity, ForceMode.VelocityChange);
        }
        else
        {
            aiDriver.SetRecovering();
        }
        // Pequeno delay para evitar loops de respawn
        Invoke(nameof(ResetRespawnFlag), 1f);
    }

    private void ResetRespawnFlag()
    {
        _isRespawning = false;
        _airTimer = 0;
    }

    private Transform GetRespawnTarget()
    {
        if (racerStatus == null)
        {
            racerStatus = GetComponent<RacerStatus>();
        } 

        if(racerStatus.waypoints.Count == 0)
            return null;

        int index = (indexAtSavePoint + 15) % racerStatus.waypoints.Count;
        return racerStatus.waypoints[index];
    }

    private Vector3 GetCurrentPoint()
    {
        if (racerStatus == null)
        {
            racerStatus = GetComponent<RacerStatus>();
        } 

        int index = racerStatus.currentWaypointIndex;

        indexAtSavePoint = index;

        Vector3 point = new Vector3(racerStatus.waypoints[index].position.x, (racerStatus.waypoints[index].position.y + racerStatus.waypoints[index].up.y * 1.25f), racerStatus.waypoints[index].position.z);
        return point;
    }

    #endregion

    #region nitro

    public void ActivateTurbo() 
    {
        // Só ativa se: tiver carga, não estiver ativo e o cooldown passou
        if (NOS_amount > 0 && !_isTurboActive && Time.time >= _nextTurboTime) 
        {
            NOS_amount--; // Consome a carga imediatamente
            _isTurboActive = true;
            turboTimer = 0f; // Reseta o cronômetro do burst inicial

            // O "Soco" inicial de velocidade
            rb.AddForce(transform.forward * turboInitialImpulse, ForceMode.VelocityChange);

            // Agenda o desligamento automático
            Invoke(nameof(StopTurbo), turboDuration);
            
            OnTurboStart?.Invoke();
        }
    }

    private void StopTurbo() 
    {
        _isTurboActive = false;
        _nextTurboTime = Time.time + _turboCooldownTime;
        OnTurboEnd?.Invoke();
    }

    private void ApplyTurboPhysics()
    {
        if (_isTurboActive)
        {
            turboTimer += Time.deltaTime;

            // Multiplicador de Burst (primeiros 0.8s mais fortes)
            float burst = (turboTimer < 0.8f) ? turboBurstForce : 1.0f;

            // Força constante de aceleração durante o turbo
            Vector3 nosForce = transform.forward * (acceleration * burst * rubberBandingFactor);
            rb.AddForce(nosForce, ForceMode.Acceleration);
        }
    }

    public void GainNOS(int qnt)
    {
        NOS_amount += qnt;
        Mathf.Clamp(NOS_amount, 0, max_NOS_amount);
    }

    #endregion

    #region Status checking

    private void GroundCheck()
    {
        int tempGroundedWheels = 0;

        for (int i = 0; i < wheelsGrounded.Length; i++)
        {
            tempGroundedWheels += wheelsGrounded[i];
        }

        isGrounded = (tempGroundedWheels >= MIN_WHEELS_TO_CONSIDERE_GROUNDED) ? true : false;

        if (isGrounded && IsAreaSafe())
        {
            _lastSafePosition = GetCurrentPoint();
            // No modo Hover, garantimos que ele respawne um pouco acima do chão
            if (isHover) _lastSafePosition += transform.up * 1f;
                
            _lastSafeRotation = transform.rotation;
            _saveTimer = 0;
        }

        if (!wasGrounded && isGrounded && !isHover)
        {
            OnLand?.Invoke(airTime);
            airTime = 0f;
        }
        else if (!isGrounded)
        {
            airTime += Time.deltaTime;
        }

        if (wasGrounded && !isGrounded && !isHover)
        {
            OnJump?.Invoke(0f);
        }

        wasGrounded = isGrounded;
    }

    private void CalculateCarVelocity()
    {
        currentCarLocalVelocity = transform.InverseTransformDirection(rb.linearVelocity);
        carVelocityRatio = currentCarLocalVelocity.z / maxSpeed;

        speedKMH = currentCarLocalVelocity.z * 3.6f;
    }
    
    #endregion

    #region AI status changing Methods

    public void SetMaxSpeed(float value)
    {
        maxSpeed = value;
    }


    #endregion

    #region raycast system

    private void UpdateGroundSensors()
    {
        cachedSurfaceNormal = Vector3.zero;
        cachedAverageHeight = 0f;
        cachedGroundedCount = 0;

        float rayLength = restLenght + springTravel + wheelRadius;
        Vector3 rayDir = -transform.up;

        for (int i = 0; i < rayPoints.Length; i++)
        {
            int hits = Physics.RaycastNonAlloc(
                rayPoints[i].position,
                rayDir,
                _raycastBuffer,
                rayLength,
                drivable
            );

            if (hits > 0)
            {
                RaycastHit hit = _raycastBuffer[0];

                groundSensors[i].hit = true;
                groundSensors[i].hitInfo = hit;

                cachedSurfaceNormal += hit.normal;
                cachedAverageHeight += hit.distance;
                cachedGroundedCount++;
            }
            else
            {
                groundSensors[i].hit = false;
            }
        }

        if (cachedGroundedCount > 0)
        {
            cachedSurfaceNormal.Normalize();
            cachedAverageHeight /= cachedGroundedCount;
        }
        else
        {
            cachedSurfaceNormal = transform.up;
        }
    }
    #endregion
}
public enum CarType {classic, hover};