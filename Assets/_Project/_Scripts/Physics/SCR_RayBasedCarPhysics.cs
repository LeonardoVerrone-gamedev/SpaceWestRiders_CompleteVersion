using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using Unity.VisualScripting;
using System;

[RequireComponent(typeof(Rigidbody))]
public class SCR_RayBasedCarPhysics : MonoBehaviour
{
    public CarType carType;
    private bool isHover;
    public bool canSwitchType = false;
    [HideInInspector] public bool AIControlled = false;
    RacerStatus racerStatus;

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

    #region Basic Setup
    private int MIN_WHEELS_TO_CONSIDERE_GROUNDED = 3;
    #endregion

    #region Suspension System
    [Header("Suspension System")]
    [SerializeField] Transform[] rayPoints;
    [SerializeField] float restLenght = 1f;
    [SerializeField] float hoverDistance = 2f;
    [SerializeField] float springTravel = 2.5f;
    [SerializeField] float springStiffness = 8000f;
    [SerializeField] float wheelRadius = 0.33f;
    [SerializeField] float hoverDamper = 8000f;
    [SerializeField] float classicDamper = 3500f;
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
    private RaycastHit[] _raycastBuffer = new RaycastHit[8];
    #endregion

    #region Hover Downforce
    [Header("Hover Downforce (Aero)")]
    [SerializeField] private float hoverDownforceAmount = 2500f;
    [HideInInspector] [SerializeField] private float minHeightThreshold = 0.4f;
    [HideInInspector] [SerializeField] private bool useDynamicDownforce = true;
    private float[] hoverHeightErrorCache = new float[4];
    #endregion

    #region Aerodynamics
    [Header("Aerodynamics (Air Drag)")]
    [SerializeField] float hoverAirDrag = 0.25f;
    [SerializeField] float classicAirDrag = 0.08f;
    [HideInInspector] [SerializeField] float dragThreshold = 50f;
    private float currentAirDrag;
    [SerializeField] private float downforceAmount = 500f;
    #endregion

    [SerializeField] private float forceLevelingDelay = 0.15f;
    private float airborneTimer = 0f;

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

    #region Balancing Curves
    [SerializeField] private AnimationCurve hoverCarTurningCurve;
    [SerializeField] private AnimationCurve classicCarTurningCurve;
    [HideInInspector][SerializeField] AnimationCurve turningCurve;
    #endregion

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
    private bool _isDrifting = false;
    private float _currentDriftAngle = 0f;
    private float _driftBoostTimer = 0f;
    private bool _isDriftBoostActive = false;
    private float _originalDragCoefficient;
    private float _originalMaxSpeed;
    #endregion

    #region Public Getters
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

    #region Body Tilt
    [Header("Body Tilt Settings")]
    [HideInInspector][SerializeField] float maxPitchAngle = 5f;
    [HideInInspector][SerializeField] float maxRollAngle = 10f;
    [HideInInspector][SerializeField] float tiltResponseSpeed = 5f;
    [HideInInspector][SerializeField] float tiltReturnSpeed = 3f;
    [HideInInspector][SerializeField] float brakeTiltMultiplier = 1.5f;
    [HideInInspector][SerializeField] AnimationCurve speedTiltCurve;
    #endregion

    #region Visual Culling
    [Header("Visual Culling")]
    public bool CanUpdateVisuals { get; private set; } = true;
    #endregion

    #region Gravity & Ground Hugging
    [Header("Gravity/Ground Hugging")]
    float gravityStrength = 9.81f * 2;
    [HideInInspector][SerializeField] float surfaceAlignmentSpeed = 10f;
    [HideInInspector][SerializeField] float groundHugDistance = 1.5f;
    [HideInInspector] public float extraGripModifier = 1.0f;
    public Vector3 _currentCarUp = Vector3.up;
    private Vector3 currentCarLocalVelocity = Vector3.zero;
    private float carVelocityRatio = 0;
    #endregion

    #region Visual Variables
    [HideInInspector][SerializeField] private float tireRorationSpeed = 3000f;
    [SerializeField] private float maxSteerAngle = 30f;
    [Header("Hover Visual Transformation")]
    [HideInInspector][SerializeField] float transitionSpeed = 5f;
    [SerializeField] Vector3 wheelHoverRotation = new Vector3(0, 180, 90);
    [SerializeField] Vector3 wheelClassicRotation = new Vector3(0, 180, 0);
    private float _hoverTransitionAlpha = 0f;
    private float _rearWheelRotationAccumulator = 0f;
    private float _frontWheelRotationAccumulator = 0f;
    private Coroutine _transitionCoroutine;
    private Quaternion _currentBaseWheelRot;
    #endregion

    #region Respawn System
    [Header("Respawn System")]
    [SerializeField]private float airTimeThreshold = 3f;
    [HideInInspector][SerializeField] private float respawnBoostIntensity = 1.5f;
    private Vector3 _lastSafePosition;
    private Quaternion _lastSafeRotation;
    private float _saveTimer;
    private float _airTimer;
    private bool _isRespawning = false;
    int indexAtSavePoint;
    #endregion

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
    Vector3[] smoothedNormals;
    public int GetNOSAmount() => NOS_amount;
    public int GetMaxNOSAmount() => max_NOS_amount;
    #endregion

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
    private Vector3 _meshLocalEulerAngles;
    private float _currentRoll;
    private float _currentPitch;
    private float _targetPitch = 0f;
    private float _targetRoll = 0f;
    private float _currentBodyPitch = 0f;
    private float _currentBodyRoll = 0f;
    #endregion

    #region Collision Setup
    [Header("Controle de Shake de Colisão")]
    private bool enableCollisionShake = true;
    private float shakeIntensity = 0.3f;
    private float minForceToShake = 0.4f;
    private float shakeCooldown = 0.4f;
    private float lastCollisionShakeTime = 0f;
    [Header("Filtro de Direção de Colisão")]
    [Tooltip("Ângulo máximo para considerar colisão frontal/traseira (graus)")]
    [Range(0, 90)]
    [SerializeField] float maxFrontalAngle = 30f;
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
    bool OnForceDrift;
    #endregion

    #region Input Handling
    public void SetSteering(float value) => _currentSteerInput = Mathf.Clamp(value, -1f, 1f);
    public void SetThrottle(float value) => _currentThrottleInput = Mathf.Clamp(value, -1f, 1f);
    public void SetHandbrake(bool value)
    {
        if (value && !_currentHandbrakeInput && isGrounded) TryStartDrift();
        else if (!value && _currentHandbrakeInput && _isDrifting) EndDrift(true);
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
    public event Action<float> OnHardAcceleration;
    public event Action<float> OnHardBrake;
    public event Action<float> OnDriftStart;
    public event Action OnDriftEnd;
    public event Action<float, Vector3> OnCollision;
    public event Action<bool> OnCrash;
    public event Action<float> OnJump;
    public event Action<float> OnLand;
    public event Action OnUpGear;
    #endregion

    #region Gear Variables
    [Header("Arcade Gearbox")]
    [SerializeField] private int totalGears = 5;
    [SerializeField] private float gearShiftCooldown = 0.15f;
    [SerializeField] private float gearAccelerationDrop = 0.85f;
    bool isShifting = false;
    [SerializeField] private float minTimeBetweenShifts = 0.5f;
    private float lastShiftTime = -999f;
    [SerializeField] private float[] shiftUpThresholds = { 0f, 50f, 100f, 150f, 200f, 300f, 400f };
    [SerializeField] private float[] shiftDownThresholds = { 0f, 40f, 90f, 140f, 190f, 290f, 390f };
    [Header("Fake Engine")]
    [SerializeField] private float minRPM = 3000f;
    [SerializeField] private float maxRPM = 9000f;
    [SerializeField] private float upshiftRPM = 8500f;
    [SerializeField] private float downshiftRPM = 3500f;
    [SerializeField] private float engineInertia = 8f;
    public float engineRPM { get; private set; }
    [SerializeField] public int currentGear { get; private set; } = 1;
    private float lastGearShiftTime = -999f;
    #endregion

    #region Crash
    private SCR_CarCrashPhysics carCrash;
    public bool crashing = false;
    [SerializeField] LayerMask crashable;
    [SerializeField] float crashImpactForce = 20f;
    SCR_MeshDeformer deformer;
    float[] groundStickTimer;
    float[] smoothedDistances;
    float[] previousCompression;
    DamageCar damageCar;

    private Vector3[] wheelOffsets;
    #endregion

    #region Slam & Squash

    [Header("Slam & Squash")]
    [SerializeField] float slamDuration = 0.08f;
    [SerializeField] float recoverDuration = 0.15f;

    [SerializeField] float landSquashMultiplier = 0.015f;
    [SerializeField] float collisionSquashMultiplier = 0.025f;

    [SerializeField] float maxSquash = 0.25f;

    private Coroutine squashRoutine;
    private Vector3 originalBodyScale;

    #endregion

    #region Unity Lifecycle
    void Awake()
    {
        _originalDragCoefficient = dragCoefficient;
        _originalMaxSpeed = maxSpeed;
        carCrash = GetComponent<SCR_CarCrashPhysics>();
        deformer = GetComponent<SCR_MeshDeformer>();

        isCameraman = GetComponent<SCR_CameramanAI>();
    }

    void Start()
    {
        if(rb == null) rb = GetComponent<Rigidbody>();
        if(carBody == null)
        {
            Debug.LogWarning("CarBody não atribuído. Usando transform do GameObject.");
            carBody = transform;
        }
        rb.useGravity = false;
        groundSensors = new GroundSensor[rayPoints.Length];
        smoothedNormals = new Vector3[rayPoints.Length];
        groundStickTimer = new float[rayPoints.Length];
        smoothedDistances = new float[rayPoints.Length];
        previousCompression = new float[rayPoints.Length];
        SwitchToMode(carType);
        damageCar = GetComponent<DamageCar>();
        if(damageCar != null) damageCar.ResetDamage();

        wheelOffsets = new Vector3[tires.Length];

        for(int i = 0; i < tires.Length; i++)
        {
            wheelOffsets[i] =
                tires[i].transform.position -
                rayPoints[i].position;
        }

        originalBodyScale = carBody.localScale;

        OnLand += SlamFromLanding;
        OnCollision += SlamFromCollision;
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
        if(!crashing)
        {
            UpdateGroundSensors();
            if (isGrounded)
            {
                UpdateGravityDirection();
                ApplyAngularDamping();
                AlignToTrack();
            }
            ApplyDownforce();
            ApplySuspension();
            GroundCheck();
            HandleMovement();
            CalculateCarVelocity();
            Visuals();
            UpdateBodyTilt();
            if (_isDrifting) ApplyDriftForces();
            if (_isDrifting) LimitDriftAngle();
            HandleRespawnSystem();
            LimitVelocity();
        }
        UpdateFakeRPM();
        UpdateAutomaticGears();
    }
    #endregion

    #region Setup
    public void SetAI(bool isAI, AIRacingController driver)
    {
        AIControlled = isAI;
        if(isAI) aiDriver = driver;
        SwitchToMode(carType);
    }
    #endregion

    #region Gear Change
    private void UpdateAutomaticGears()
    {
        if (!isGrounded || isShifting) return;
        if (Time.time < lastShiftTime + minTimeBetweenShifts) return;
        int targetGear = currentGear;
        if (_isTurboActive)
        {
            targetGear = 7;
        }
        else if(Mathf.Round(speedKMH) == 0)
        {
            targetGear = 0;
        }
        else
        {
            if (currentGear < totalGears && engineRPM >= upshiftRPM) targetGear = currentGear + 1;
            else if (currentGear > 1 && engineRPM <= downshiftRPM) targetGear = currentGear - 1;
        }
        if (targetGear != currentGear) StartCoroutine(GearShiftCoroutine(targetGear));
    }

    private IEnumerator GearShiftCoroutine(int newGear)
    {
        isShifting = true;
        lastShiftTime = Time.time;
        float originalDrop = gearAccelerationDrop;
        gearAccelerationDrop = 0.4f;
        
        if(newGear > currentGear)
        {
            OnUpGear?.Invoke();
        }

        yield return new WaitForSeconds(gearShiftCooldown);
        currentGear = newGear;
        gearAccelerationDrop = originalDrop;
        isShifting = false;
    }

    private void UpdateFakeRPM()
    {
        float forwardSpeed = Mathf.Max(0f, currentCarLocalVelocity.z);
        float effectiveMaxSpeed = AIControlled ? maxSpeed * rubberBandingFactor : maxSpeed;
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

    #region Transition Between Car Modes
    public void ToggleVehicleMode()
    {
        if (!canSwitchType) return;
        SwitchToMode(carType == CarType.classic ? CarType.hover : CarType.classic);
    }

    public void SwitchToMode(CarType newMode)
    {
        carType = newMode;
        isHover = (carType == CarType.hover);
        MIN_WHEELS_TO_CONSIDERE_GROUNDED = isHover ? 3 : 3;
        dragCoefficient = isHover ? hoverCarDragCoefficient : classicCarDragCoefficient;
        acceleration = isHover ? hoverCarAcceleration : classicCarAcceleration;
        maxSpeed = isHover ? hoverCarMaxSpeed : classicCarMaxSpeed;
        turningCurve = isHover ? hoverCarTurningCurve : classicCarTurningCurve;
        currentAirDrag = isHover ? hoverAirDrag : classicAirDrag;
        currentTargetSuspensionLength = isHover ? hoverDistance : restLenght;
        currentDamper = isHover ? hoverDamper : classicDamper;
        if (isHover) rb.AddForce(transform.up * 5f, ForceMode.VelocityChange);
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
            if(_currentThrottleInput > 0.1f) Acceleration();
            if(_currentThrottleInput < -0.1f) Deaceleration();
            SidewaysDrag();
            if(carType == CarType.hover) ApplyAirDrag();
        }
        else AirControl();
        Turn();
    }

    private void LimitVelocity()
    {
        Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);
        float maxLimit = _isTurboActive ? maxSpeed * turboMaxSpeedMultiplier : maxSpeed;
        maxLimit *= rubberBandingFactor;
        float minLimit = -(maxSpeed / 3f);
        localVel.z = Mathf.Clamp(localVel.z, minLimit, maxLimit);
        //rb.linearVelocity = transform.TransformDirection(localVel);
    }

    private void Acceleration()
    {
        if(isShifting) return;
        float effectiveMaxSpeed = maxSpeed;
        if (AIControlled) effectiveMaxSpeed *= rubberBandingFactor;
        if (_isTurboActive) effectiveMaxSpeed *= turboMaxSpeedMultiplier;
        if (Mathf.Abs(currentCarLocalVelocity.z) >= effectiveMaxSpeed) return;
        float currentAcceleration = acceleration;
        float gearFactor = Mathf.Lerp(1.25f, 0.8f, (float)(currentGear - 1) / (totalGears - 1));
        currentAcceleration *= gearFactor * gearAccelerationDrop;
        if (_isDrifting) currentAcceleration *= driftAccelerationMultiplier;
        if (_isTurboActive) currentAcceleration *= turboAccelMultiplier;
        if (_isDriftBoostActive) currentAcceleration *= 2f;
        if (AIControlled) currentAcceleration *= rubberBandingFactor;
        rb.AddForceAtPosition(currentAcceleration * _currentThrottleInput * transform.forward, accelerationPoint.position, ForceMode.Acceleration);
        if (Time.time > _nextHardAccelTime && _currentThrottleInput > 0.9f && currentCarLocalVelocity.z < maxSpeed * 0.5f)
        {
            OnHardAcceleration?.Invoke(currentCarLocalVelocity.z / maxSpeed);
            _nextHardAccelTime = Time.time + hardAccelCooldown;
        }
    }

    private void Deaceleration()
    {
        if(isShifting) return;
        float brakeMultiplier = 1f;
        if(currentCarLocalVelocity.z > 1f && !AIControlled) brakeMultiplier = 1.1f;
        if(currentCarLocalVelocity.z < 0.1f && Mathf.Abs(currentCarLocalVelocity.z) >= maxSpeed / 3) return;
        float currentDeceleration = deceleration;
        if (AIControlled) currentDeceleration *= rubberBandingFactor;
        rb.AddForceAtPosition(currentDeceleration * brakeMultiplier * _currentThrottleInput * transform.forward, accelerationPoint.position, ForceMode.Acceleration);
        if (Time.time > _nextHardBrakeTime && _currentThrottleInput < -0.9f && currentCarLocalVelocity.z > 10f)
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
        if (_isDrifting)
        {
            if (Mathf.Abs(steerInput) < 0.1f)
            {
                float selfSteerFactor = -(_currentDriftAngle / maxDriftAngle);
                float TimeMultiplier = carType == CarType.classic ? 1f : Time.fixedDeltaTime;
                steerInput = selfSteerFactor * driftSelfSteerStrength * TimeMultiplier;
                steerInput = Mathf.Clamp(steerInput, -driftCounterSteerLimit, driftCounterSteerLimit);
            }
            else if (Mathf.Sign(steerInput) != Mathf.Sign(_currentDriftAngle))
            {
                steerPower *= 1.25f;
            }
        }
        float accelerationSteerBoost = 1f + (_currentThrottleInput * 0.3f);
        float speedFactor = turningCurve.Evaluate(Mathf.Abs(carVelocityRatio));

        float damage01 = GetDamage01();

        // Converte o dano em intensidade de instabilidade.
        //
        // 0–20%  = praticamente normal
        // 20–100% = instabilidade progressiva
        float damageInstability =
            Mathf.InverseLerp(0.20f, 1f, damage01);

        damageInstability =
            Mathf.SmoothStep(0f, 1f, damageInstability);

        // Wobble proporcional ao dano.
        float damageWobble =
            GetDamageSteeringWobble(damageInstability);

        // Aplica o wobble na direção do jogador.
        float unstableSteerInput =
            steerInput + damageWobble;

        unstableSteerInput = Mathf.Clamp(unstableSteerInput, -1f, 1f);

        if (Mathf.Abs(unstableSteerInput) < 0.1f && isGrounded && !_isDrifting)
        {
            Vector3 localAngularVel = transform.InverseTransformDirection(rb.angularVelocity);
            localAngularVel.y *= 0.9f;
            rb.angularVelocity = transform.TransformDirection(localAngularVel);
        }
        Vector3 turnAxis = cachedSurfaceNormal;
        if(carType == CarType.classic && !_isDrifting)
            rb.AddTorque(turnAxis * steerPower * unstableSteerInput * speedFactor * accelerationSteerBoost, ForceMode.VelocityChange);
        else
            rb.AddTorque(turnAxis * steerPower * unstableSteerInput * speedFactor * accelerationSteerBoost, ForceMode.Acceleration);
    }

    [Header("Damage Instability")]
    [SerializeField, Range(0f, 1f)]
    private float maxDamageInstability = 1f;

    [SerializeField]
    private float damageWobbleFrequency = 3.5f;

    [SerializeField]
    private float damageWobbleStrength = 0.18f;

    private float GetDamage01()
    {
        if (damageCar == null)
            return 0f;

        return damageCar.Damage01;
    }

    private float GetDamageSteeringWobble(float damageInstability)
    {
        if (damageInstability <= 0f)
            return 0f;

        float wobble =
            Mathf.Sin(Time.time * damageWobbleFrequency)
            * damageWobbleStrength;

        return wobble * damageInstability * maxDamageInstability;
    }

    private void SidewaysDrag()
    {
        Vector3 planarVelocity =
            Vector3.ProjectOnPlane(rb.linearVelocity, cachedSurfaceNormal);

        Vector3 lateralDir =
            Vector3.Cross(cachedSurfaceNormal, transform.forward).normalized;

        float lateralSpeed =
            Vector3.Dot(planarVelocity, lateralDir);

        float damage01 = GetDamage01();

        float damageInstability =
            Mathf.InverseLerp(0.20f, 1f, damage01);

        damageInstability =
            Mathf.SmoothStep(0f, 1f, damageInstability);

        float gripMultiplier =
            Mathf.Lerp(1f, 0.45f, damageInstability);

        float dragForceAmount =
            -lateralSpeed *
            dragCoefficient *
            gripMultiplier;

        Vector3 dragForce =
            lateralDir * dragForceAmount;

        rb.AddForce(
            dragForce,
            ForceMode.Acceleration
        );

        if (carType == CarType.classic &&
            isGrounded &&
            Mathf.Abs(_currentSteerInput) > 0.1f)
        {
            float speedFactor =
                planarVelocity.magnitude / maxSpeed;

            Vector3 gripForce =
                lateralDir *
                _currentSteerInput *
                speedFactor *
                gripMultiplier;

            rb.AddForce(
                gripForce,
                ForceMode.Acceleration
            );
        }
    }
    #endregion

    #region Suspension
    void ApplySuspension()
{
    if (isHover)
    {
        ApplyHoverSuspension();
    }
    else
    {
        ApplyClassicSuspension();
    }
}

private void ApplyHoverSuspension()
{
    float targetDistance = currentTargetSuspensionLength;
    float damperStiffness = currentDamper;
    
    for (int i = 0; i < rayPoints.Length; i++)
    {
        if (!groundSensors[i].hit && groundStickTimer[i] <= 0f)
        {
            wheelsGrounded[i] = 0;
            if(tires != null && tires.Length > 0)  SetTirePosition(tires[i], rayPoints[i].position);
            continue;
        }
        
        wheelsGrounded[i] = 1;
        RaycastHit hit = groundSensors[i].hitInfo;
        
        // Erro de altura
        float heightError = targetDistance - hit.distance;
        
        // Smoothing do erro (remove jitter)
        heightError = Mathf.Lerp(hoverHeightErrorCache[i], heightError, 0.25f);
        hoverHeightErrorCache[i] = heightError;
        
        // Velocidade vertical da roda
        float hoverSpringVelocity = Vector3.Dot(rb.GetPointVelocity(rayPoints[i].position), cachedSurfaceNormal);
        
        // Força base (controle de altura)
        float liftForce = heightError * springStiffness;
        
        // Curva não-linear (seu estilo original)
        liftForce *= 1f + Mathf.Abs(heightError) * 0.5f;
        
        // Damping dinâmico
        float dynamicDamper = damperStiffness * (1f + Mathf.Abs(hoverSpringVelocity) * 0.5f);
        float dampForce = dynamicDamper * hoverSpringVelocity;
        
        float netForce = liftForce - dampForce;
        
        // Clamp suave
        float maxHoverForce = rb.mass * 8f;
        netForce = Mathf.Clamp(netForce, -maxHoverForce, maxHoverForce);
        
        // Aplica força na normal do chão
        rb.AddForceAtPosition(netForce * cachedSurfaceNormal, rayPoints[i].position);
        
        // Visual
        if(tires != null && tires.Length > 0) SetTirePosition(tires[i], rayPoints[i].position);
    }
    
    // Gravidade reduzida para hover
    rb.AddForce(-cachedSurfaceNormal * rb.mass * gravityStrength * 0.6f);
}

private void ApplyClassicSuspension()
{
    float targetDistance = currentTargetSuspensionLength;
    float damperStiffness = currentDamper;
    float maxVisualLenght = 0.275f;
    
    for (int i = 0; i < rayPoints.Length; i++)
    {
        if (!groundSensors[i].hit && groundStickTimer[i] <= 0f)
        {
            wheelsGrounded[i] = 0;
            Vector3 airPos = rayPoints[i].position - transform.up * maxVisualLenght;
            if(tires != null && tires.Length > 0) SetTirePosition(tires[i], airPos);
            continue;
        }
        
        wheelsGrounded[i] = 1;
        RaycastHit hit = groundSensors[i].hitInfo;
        
        float currentSpringLength = hit.distance;
        float compression = (targetDistance - currentSpringLength) / springTravel;
        compression = Mathf.Clamp(compression, -0.3f, 0.6f);
        
        // Força da mola normal
        float springForce = compression * springStiffness;
        float springVelocity = Vector3.Dot(rb.GetPointVelocity(rayPoints[i].position), hit.normal);
        float dampForce = springVelocity * damperStiffness * 1.5f;
        
        float netForce = springForce - dampForce;
        
        // Limites
        netForce = Mathf.Clamp(netForce, -rb.mass * 15f, rb.mass * 10f);
        
        if (Mathf.Abs(netForce) > 0.1f)
        {
            rb.AddForceAtPosition(netForce * hit.normal, rayPoints[i].position);
        }
        
        // Visual
        float visualCompression = Mathf.Clamp(compression, 0f, 0.35f);
        float visualOffset = visualCompression * 0.2f;
        Vector3 visualPos = rayPoints[i].position - transform.up * visualOffset;
        if(tires != null && tires.Length > 0) SetTirePosition(tires[i], visualPos);
    }
    
    rb.AddForce(-cachedSurfaceNormal * rb.mass * gravityStrength);
}
    #endregion

    #region Visuals
    public void SetVisualState(bool canUpdate) => CanUpdateVisuals = canUpdate;

    private void Visuals()
    {
        if(!CanUpdateVisuals) return;
        TireVisuals();
    }

    private void TireVisuals()
    {
        if (isHover) return;
        float steeringAngle = maxSteerAngle * _currentSteerInput;
        if(Mathf.Abs(_currentThrottleInput ) > 0.1f) _rearWheelRotationAccumulator += tireRorationSpeed * _currentThrottleInput * Time.fixedDeltaTime;
        else _rearWheelRotationAccumulator += tireRorationSpeed * carVelocityRatio * Time.fixedDeltaTime;
        _frontWheelRotationAccumulator += tireRorationSpeed * carVelocityRatio * Time.fixedDeltaTime;
        for (int i = 0; i < tires.Length; i++)
        {
            if (tires[i] == null) continue;
            if (i >= 2) tires[i].transform.localRotation = _currentBaseWheelRot * Quaternion.Euler(_rearWheelRotationAccumulator, 0, 0);
            else
            {
                tires[i].transform.localRotation = _currentBaseWheelRot * Quaternion.Euler(_frontWheelRotationAccumulator, 0, 0);
                if (frontTiresParent[i] != null) frontTiresParent[i].transform.localRotation = Quaternion.Euler(0, steeringAngle, 0);
            }
        }
    }

    private IEnumerator AnimateWheelTransition()
    {
        float targetAlpha = (carType == CarType.hover) ? 1f : 0f;
        Quaternion startRot = Quaternion.Euler(wheelClassicRotation);
        Quaternion endRot = Quaternion.Euler(wheelHoverRotation);
        _currentBaseWheelRot = startRot;
        while (!Mathf.Approximately(_hoverTransitionAlpha, targetAlpha))
        {
            _hoverTransitionAlpha = Mathf.MoveTowards(_hoverTransitionAlpha, targetAlpha, Time.deltaTime * transitionSpeed);
            _currentBaseWheelRot = Quaternion.Slerp(startRot, endRot, _hoverTransitionAlpha);
            ApplyBaseTransformations();
            yield return null;
        }
        _currentBaseWheelRot = (carType == CarType.hover) ? endRot : startRot;
        ApplyBaseTransformations();
        _transitionCoroutine = null;
    }

    private void ApplyBaseTransformations()
    {
        for (int i = 0; i < tires.Length; i++)
        {
            if (tires[i] == null) continue;
            tires[i].transform.localRotation = _currentBaseWheelRot;
        }
    }
    
    private void UpdateBodyTilt()
    {
        if (carBody == null || !CanUpdateVisuals) return;
        float currentSpeed = rb.linearVelocity.magnitude;
        float speedFactor = speedTiltCurve.Evaluate(Mathf.Clamp01(currentSpeed / maxSpeed));
        if (_isTurboActive)
        {
            float turboIntensity = 1.5f;
            _targetPitch = -maxPitchAngle * turboIntensity;
            _targetRoll = (-maxRollAngle * _currentSteerInput * speedFactor) * 0.3f;
            _targetPitch += UnityEngine.Random.Range(-0.5f, 0.5f);
        }
        else
        {
            float driftRollMultiplier = _isDrifting ? 2f : 1f;
            if (_currentThrottleInput > 0.1f) _targetPitch = -maxPitchAngle * _currentThrottleInput * speedFactor;
            else if (_currentThrottleInput < -0.1f && rb.linearVelocity.z > 1f) _targetPitch = maxPitchAngle * _currentThrottleInput * brakeTiltMultiplier * speedFactor;
            else _targetPitch = 0f;
            if (Mathf.Abs(_currentSteerInput) > 0.1f && currentSpeed > 1f) _targetRoll = -maxRollAngle * _currentSteerInput * speedFactor * driftRollMultiplier;
            else _targetRoll = 0f;
        }
        float currentResponseSpeed = (Mathf.Abs(_targetPitch) > Mathf.Abs(_currentBodyPitch) || Mathf.Abs(_targetRoll) > Mathf.Abs(_currentBodyRoll)) ? tiltResponseSpeed : tiltReturnSpeed;
        _currentBodyPitch = Mathf.Lerp(_currentBodyPitch, _targetPitch, Time.deltaTime * currentResponseSpeed);
        _currentBodyRoll = Mathf.Lerp(_currentBodyRoll, _targetRoll, Time.deltaTime * currentResponseSpeed);
        float currentYaw = carBody.localEulerAngles.y;
        carBody.localEulerAngles = new Vector3(_currentBodyPitch, currentYaw, _currentBodyRoll);
    }

    private void SetTirePosition(GameObject tire, Vector3 targetPosition)
    {
        if(!CanUpdateVisuals) return;
        tire.transform.position = targetPosition;
    }

    private void SlamFromLanding(float airTime)
    {
        rb.AddForce(
            -transform.up * 5f,
            ForceMode.Impulse
        );

        float intensity = Mathf.Clamp01(airTime * landSquashMultiplier);

        if (intensity > 0.01f)
            SlamSquash(intensity);
    }

    private void SlamFromCollision(float force, Vector3 dir)
    {
        float intensity = Mathf.Clamp01(force * collisionSquashMultiplier);

        if (intensity > 0.01f)
            SlamSquash(intensity);
    }

    public void SlamSquash(float intensity)
    {
        if (carBody == null) return;

        intensity = Mathf.Clamp(intensity, 0f, maxSquash);

        if (squashRoutine != null)
            StopCoroutine(squashRoutine);

        squashRoutine = StartCoroutine(SlamSquashRoutine(intensity));
    }

    private IEnumerator SlamSquashRoutine(float intensity)
    {
        Vector3 squashScale = new Vector3(
            originalBodyScale.x + intensity * 0.4f,
            originalBodyScale.y - intensity,
            originalBodyScale.z + intensity * 0.4f
        );

        float t = 0f;

        while (t < slamDuration)
        {
            t += Time.deltaTime;

            carBody.localScale = Vector3.Lerp(
                originalBodyScale,
                squashScale,
                t / slamDuration
            );

            yield return null;
        }

        t = 0f;

        while (t < recoverDuration)
        {
            t += Time.deltaTime;

            carBody.localScale = Vector3.Lerp(
                squashScale,
                originalBodyScale,
                t / recoverDuration
            );

            yield return null;
        }

        carBody.localScale = originalBodyScale;
    }
    #endregion

    #region Physics - Stability
    Vector3 _currentDownDir;
    public void ApplyDownforce()
    {
        float speed = rb.linearVelocity.magnitude;
        if (!isHover)
        {
            float speedRatio = speed / maxSpeed;
            float df = speedRatio * speedRatio * downforceAmount;
            float maxForce = rb.mass * Mathf.Abs(Physics.gravity.y) * 10f;
            df = Mathf.Clamp(df, 0f, maxForce);
            Vector3 targetDir = cachedGroundedCount > 0
                ? cachedSurfaceNormal
                : lastGroundNormal;
                
            _currentDownDir = targetDir;
            rb.AddForce(-_currentDownDir * df, ForceMode.Force);
            float extraStick = rb.mass * Mathf.Lerp(4f, 20f, speedRatio);
            rb.AddForce(-_currentDownDir * extraStick, ForceMode.Force);
            return;
        }
        if (cachedGroundedCount == 0) return;
        float heightRatio = cachedAverageHeight / restLenght;
        if (heightRatio < minHeightThreshold) return;
        float speedFactor = useDynamicDownforce ? speed / (maxSpeed / 3.6f) : 1f;
        Vector3 downDir = cachedGroundedCount > 0
                ? cachedSurfaceNormal
                : lastGroundNormal;;

        rb.AddForce(-downDir * hoverDownforceAmount * speedFactor, ForceMode.Force);
    }

    private void ApplyAirDrag()
    {
        float forwardSpeed = currentCarLocalVelocity.z;
        float speedRatio = Mathf.Clamp01(forwardSpeed / maxSpeed);
        if (speedRatio < 0.7f) return;
        float dragStrength = Mathf.Lerp(0f, currentAirDrag, (speedRatio - 0.7f) / 0.3f);
        Vector3 dragForce = -transform.forward * forwardSpeed * forwardSpeed * dragStrength;
        rb.AddForce(dragForce, ForceMode.Acceleration);
    }

    private void ApplyAngularDamping()
    {
        Vector3 localAngularVelocity = transform.InverseTransformDirection(rb.angularVelocity);
        float dampingStrength = isGrounded ? 50f : 10f;
        if (_isDrifting) dampingStrength *= 0.3f;
        localAngularVelocity.x *= -dampingStrength;
        localAngularVelocity.z *= -dampingStrength;
        Vector3 dampingTorque = transform.TransformDirection(localAngularVelocity);
        rb.AddTorque(dampingTorque, ForceMode.Acceleration);
    }

    private Vector3 lastGroundNormal = Vector3.up;
    private Vector3 predictedNormal = Vector3.up;
    private void AirControl()
    {
        if (isGrounded) return;
        float speed = rb.linearVelocity.magnitude;
        float speedRatio = Mathf.Clamp01(speed / maxSpeed);
        float pitchInput = _currentThrottleInput;
        float rollInput = _currentSteerInput;
        Vector3 controlTorque = new Vector3(pitchInput, 0f, rollInput) * airControlStrength;
        rb.AddRelativeTorque(controlTorque, ForceMode.Acceleration);
        predictedNormal = lastGroundNormal;
        RaycastHit hit;
        if (Physics.Raycast(transform.position, -lastGroundNormal, out hit, 10f, drivable)) predictedNormal = Vector3.Slerp(lastGroundNormal, hit.normal, 0.3f);
        ForceLeveling();
        Vector3 targetUp = GetGravityDirection();
        rb.AddForce(targetUp * 50f, ForceMode.Acceleration);
        rb.angularVelocity = Vector3.ClampMagnitude(rb.angularVelocity, 8f);
    }

    private Vector3 GetGravityDirection()
    {
        RaycastHit hit;

        // procura pista abaixo do carro
        bool hasTrackBelow = Physics.Raycast(
            transform.position,
            -transform.up,
            out hit,
            50f,
            drivable
        );

        // se encontrou pista abaixo, continua usando gravidade local
        if (hasTrackBelow)
        {
            return -transform.up;
        }

        // se não encontrou nada, volta para gravidade global
        return -Vector3.up;
    }

    private Vector3 GetPredictedGroundNormal()
    {
        Vector3 accumulatedNormal = Vector3.zero;
        int hits = 0;

        Vector3 gravityDir = GetGravityDirection();

        float castDistance = 50f;

        for (int i = 0; i < rayPoints.Length; i++)
        {
            RaycastHit hit;

            if (Physics.Raycast(
                rayPoints[i].position,
                gravityDir,
                out hit,
                castDistance,
                drivable))
            {
                accumulatedNormal += hit.normal;
                hits++;
            }
        }

        if (hits > 0)
        {
            return (accumulatedNormal / hits).normalized;
        }

        // fallback
        return GetGravityDirection();
    }

    private void ForceLeveling()
    {
        if (isGrounded) return;

        if (airborneTimer < forceLevelingDelay) return;

        Vector3 targetUp = GetPredictedGroundNormal();

        Quaternion targetRotation =
            Quaternion.FromToRotation(transform.up, targetUp);

        targetRotation.ToAngleAxis(
            out float angle,
            out Vector3 axis
        );

        if (angle > 180f)
            angle -= 360f;

        if (Mathf.Abs(angle) < 1f)
            return;

        float autoRightStrength = 30f;

        float dot = Vector3.Dot(transform.up, targetUp);

        float inversion = Mathf.InverseLerp(1f, -1f, dot);

        autoRightStrength *= Mathf.Lerp(
            1f,
            3f,
            inversion
        );

        rb.AddTorque(
            axis.normalized *
            angle *
            Mathf.Deg2Rad *
            autoRightStrength,
            ForceMode.Acceleration
        );

        Vector3 localAV =
            transform.InverseTransformDirection(
                rb.angularVelocity
            );

        localAV.x *= 0.96f;
        localAV.z *= 0.96f;

        rb.angularVelocity =
            transform.TransformDirection(localAV);
    }

    private void UpdateGravityDirection() => _currentCarUp = cachedSurfaceNormal;

    void AlignToTrack()
    {
        Quaternion targetRotation = Quaternion.FromToRotation(transform.up, cachedSurfaceNormal) * transform.rotation;
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, Time.fixedDeltaTime * surfaceAlignmentSpeed));
    }
    #endregion

    #region Physics - Drift System
    private void TryStartDrift()
    {
        if (OnForceDrift)
        {
            if (!_isDrifting) StartDrift();
            return;
        }

        if ((!isHover && !isGrounded)) return;
        float minDriftSpeed = maxSpeed * driftEnterThreshold;
        if (rb.linearVelocity.magnitude < minDriftSpeed) return;
        if (!_isDrifting) StartDrift();
    }

    private void StartDrift()
    {
        if(_isDrifting)return;

        if (_isRestoringDrag)
        {
            dragCoefficient = _originalDragCoefficient;
            _isRestoringDrag = false;
        }

        _isDrifting = true;
        dragCoefficient = _originalDragCoefficient * driftStability;
        OnDriftStart?.Invoke(Mathf.Abs(15f));
    }

    private void UpdateDriftState()
    {
        if (!_isDrifting) return;

        if (OnForceDrift)
        {
            if (!isHover && !isGrounded) EndDrift(false);
            return;
        }

        float minDriftSpeed = maxSpeed * driftEnterThreshold * 0.3f;
        if ((!isHover && !isGrounded) || rb.linearVelocity.magnitude < minDriftSpeed) EndDrift(false);
    }

    private void CalculateDriftAngle()
    {
        Vector3 carForward = transform.forward;
        Vector3 velocityDirection = rb.linearVelocity.normalized;
        carForward.y = 0;
        velocityDirection.y = 0;
        carForward.Normalize();
        velocityDirection.Normalize();
        if (carForward.magnitude > 0.1f && velocityDirection.magnitude > 0.1f)
        {
            float rawAngle = Vector3.Angle(carForward, velocityDirection);
            float driftDirection = Mathf.Sign(Vector3.Cross(carForward, velocityDirection).y);
            _currentDriftAngle = rawAngle * driftDirection;
        }
        else _currentDriftAngle = 0f;
    }

    private void LimitDriftAngle()
    {
        if (OnForceDrift) return;

        if (Mathf.Abs(_currentDriftAngle) > maxDriftAngle)
        {
            float correctionStrength = 0.5f;
            float correctionTorque = -Mathf.Sign(_currentDriftAngle) * correctionStrength;
            rb.AddRelativeTorque(0, correctionTorque, 0, ForceMode.Acceleration);
        }
    }

   private void ApplyDriftForces()
    {
        if ((!isHover && !isGrounded) || !_isDrifting) return;
        float driftIntensity = Mathf.Clamp01(Mathf.Abs(_currentDriftAngle) / maxDriftAngle);
        float forwardSpeed = currentCarLocalVelocity.z;
        if (forwardSpeed > 5f)
        {
            float speedMaintainForce = driftIntensity * acceleration * speedMaintainForceMultiplier * 1.2f;
            rb.AddForce(transform.forward * speedMaintainForce, ForceMode.Acceleration);
        }
        if (Mathf.Abs(_currentSteerInput) < 0.1f)
        {
            Vector3 localAV = transform.InverseTransformDirection(rb.angularVelocity);
            localAV.y = Mathf.Lerp(localAV.y, 0f, Time.fixedDeltaTime * 10f);
            rb.angularVelocity = transform.TransformDirection(localAV);
        }
        float centrifugalForce = rb.linearVelocity.magnitude * driftIntensity * 0.3f;
        Vector3 forceDirection = -transform.right * Mathf.Sign(_currentDriftAngle);
        rb.AddForce(forceDirection * centrifugalForce, ForceMode.Acceleration);
        Vector3 av = transform.InverseTransformDirection(rb.angularVelocity);
        float maxRotation = 2.5f;
        av.y = Mathf.Clamp(av.y, -maxRotation, maxRotation);
        rb.angularVelocity = transform.TransformDirection(av);
    }

    private void EndDrift(bool giveBoost)
    {
        if (OnForceDrift) return;

        OnDriftEnd?.Invoke();
        _isRestoringDrag = true;
        _dragRestoreTimer = 0f;
        _isDrifting = false;
        if (giveBoost && Mathf.Abs(_currentDriftAngle) > 15f) ApplyDriftBoost();
    }

    private void UpdateDragRestoration()
    {
        if (!_isRestoringDrag) return;
        _dragRestoreTimer += Time.deltaTime;
        float lerpPercent = _dragRestoreTimer / dragRestoreDuration;
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
        if(_isTurboActive) return;
        
        _isDriftBoostActive = true;
        _driftBoostTimer = driftBoostDuration;
        Vector3 carForward = transform.forward;
        Vector3 velocityDir = rb.linearVelocity.normalized;
        Vector3 boostDirection = Vector3.Lerp(velocityDir, carForward, 0.7f).normalized;
        Vector3 boostForce = boostDirection * driftBoostForce;
        if (!racerStatus.isPlayer) return;
        rb.AddForce(boostForce, ForceMode.VelocityChange);
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

    #region Collision

    private bool IsDestroyed()
    {
        return damageCar != null && damageCar.Damage01 >= 1f;
    }

    void OnCollisionEnter(Collision collision)
{
    if (collision.gameObject == this.gameObject) return;
    if (((1 << collision.gameObject.layer) & drivable) != 0) return;

    float upDot = Vector3.Dot(cachedSurfaceNormal, Vector3.up);
    float impactForce = collision.relativeVelocity.magnitude;
    float currentSpeed = rb.linearVelocity.magnitude;

    Vector3 dir = -collision.contacts[0].normal;

    bool isCrashableObject =
        ((1 << collision.gameObject.layer) & crashable) != 0;

    // Aplica dano da colisão
    if (damageCar != null)
    {
        damageCar.ApplyDamage((0.00003f * impactForce));
        //if(racerStatus.isPlayer) damageCar.ApplyDamage((0.00002f * impactForce));
        //else damageCar.ApplyDamage((0.0000002f * impactForce));
    }

    // Verifica se essa colisão destruiu o carro
    bool isDestroyed = IsDestroyed();

    Vector3 collisionNormal = collision.contacts[0].normal;
    float speedMagnitude = collision.relativeVelocity.magnitude;

    Vector3 forceDirection =
        -collisionNormal * speedMagnitude;

    deformer?.Deform(
        collision.contacts[0].point,
        forceDirection
    );

    // =========================================================
    // COLLISION SHAKE
    // =========================================================

    if (enableCollisionShake &&
        impactForce >= minForceToShake &&
        Time.time >= lastCollisionShakeTime + shakeCooldown)
    {
        float finalForce =
            Mathf.Clamp01(impactForce / 20f) * shakeIntensity;

        OnCollision?.Invoke(finalForce, dir);

        lastCollisionShakeTime = Time.time;
    }

    // =========================================================
    // PLAYER DESTRUÍDO → CAPOTA
    // =========================================================

    if (
        isDestroyed &&
        impactForce > crashImpactForce &&
        !_isTurboActive &&
        upDot > 0.6f)
    {
        float rollDirection =
            Mathf.Sign(Vector3.Dot(transform.right, dir));

        if (Mathf.Abs(rollDirection) < 0.1f)
            rollDirection = 1f;

        StartForceDrift();
        Invoke(nameof(EndForceDrift), 4f);

        if (crashing) return;

            OnCrash?.Invoke(true);
            carCrash.TriggerCrash();

            Invoke(
                nameof(ResetCrashCam),
                carCrash.crashDuration
            );

            Invoke(nameof(GameOver), carCrash.crashDuration + 1f);

        return;
    }

    // =========================================================
    // CRASH NORMAL
    // =========================================================

    if (isCrashableObject &&
        impactForce > crashImpactForce &&
        IsFrontalCollision(dir) &&
        !_isTurboActive &&
        (ShouldAvoidCrashForSlowPlayers() == false) &&
        upDot > 0.6f)
    {
        if (racerStatus.isPlayer)
        {
            StartForceDrift();
            Invoke(nameof(EndForceDrift), 4f);
        }
        else
        {
            if (crashing) return;

            OnCrash?.Invoke(true);
            carCrash.TriggerCrash();

            Invoke(
                nameof(ResetCrashCam),
                carCrash.crashDuration
            );
        }
    }

    // =========================================================
    // SPIN
    // =========================================================

    if (impactForce > spinMinForce &&
        impactForce < spinMaxForceForCrash &&
        currentSpeed > spinMinSpeed &&
        IsLateralCollision(dir) &&
        !_isTurboActive)
    {
        if (!racerStatus.isPlayer)
        {
            TriggerSpin(dir, impactForce);
        }
        else
        {
            float spinDirection =
                Mathf.Sign(Vector3.Dot(transform.right, dir));

            StartForceDrift();

            rb.AddTorque(
                transform.up *
                spinDirection *
                spinTorqueForce *
                ((impactForce / spinMinForce) / 2),
                ForceMode.VelocityChange
            );

            Invoke(
                nameof(EndForceDrift),
                4f
            );
        }

        return;
    }
}

    public void GameOver()
    {
        // Evita executar o GameOver mais de uma vez
        if (!crashing)
            crashing = true;

        // =========================================================
        // EXPLOSÃO
        // =========================================================

        SCR_CarEffects carEffects = GetComponent<SCR_CarEffects>();

        if (carEffects != null)
            carEffects.PlayBigBoom();

        Invoke(nameof(Retire), .86f);
    }

    void Retire()
    {
        if (racerStatus != null)
            racerStatus.RetirePlayer();

        Transform bigExplosion = null;

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);

            if (child.name.Equals("Big Explosion", StringComparison.OrdinalIgnoreCase))
            {
                bigExplosion = child;
                break;
            }
        }

        // =========================================================
        // DESTROI FILHOS DO CARRO
        // =========================================================

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            Transform child = transform.GetChild(i);

            if (child.name.Equals("PlayerHUD", StringComparison.OrdinalIgnoreCase))
                continue;

            if (child == bigExplosion)
            {
                child.parent = null;
                continue;
            }

            Destroy(child.gameObject);
        }

        // =========================================================
        // DESTROI COMPONENTES DO CARRO
        // =========================================================

        Component[] components = GetComponents<Component>();

        foreach (Component component in components)
        {
            if (component == null)
                continue;

            if (component is Transform)
                continue;

            if (component is RacerStatus)
                continue;

            if (component is PlayerGameplayManager)
                continue;

            Destroy(component);
        }

        Destroy(this.GetComponent<Rigidbody>());
    }

    void StartForceDrift()
    {
        OnForceDrift = true;
        TryStartDrift();
    }

    void EndForceDrift()
    {
        OnForceDrift = false;
        if(!_currentHandbrakeInput) EndDrift(false);
    }

    bool ShouldAvoidCrashForSlowPlayers() => racerStatus.isPlayer && speedKMH <= 120f;

    private bool IsFrontalOrRearCollision(Vector3 collisionDirection)
    {
        Vector3 planarForward = Vector3.ProjectOnPlane(transform.forward, cachedSurfaceNormal);
        Vector3 planarCollision = Vector3.ProjectOnPlane(collisionDirection, cachedSurfaceNormal);
        if (planarCollision.sqrMagnitude < 0.0001f) return false;
        planarForward.Normalize();
        planarCollision.Normalize();
        float dot = Vector3.Dot(planarForward, planarCollision);
        float minDot = Mathf.Cos(maxFrontalAngle * Mathf.Deg2Rad);
        bool isFrontal = includeFrontCollisions && dot >= minDot;
        bool isRear = includeRearCollisions && dot <= -minDot;
        return isFrontal || isRear;
    }

    private bool IsFrontalCollision(Vector3 collisionDirection)
    {
        Vector3 planarForward = Vector3.ProjectOnPlane(transform.forward, cachedSurfaceNormal);
        Vector3 planarCollision = Vector3.ProjectOnPlane(collisionDirection, cachedSurfaceNormal);
        if (planarCollision.sqrMagnitude < 0.0001f) return false;
        planarForward.Normalize();
        planarCollision.Normalize();
        float dot = Vector3.Dot(planarForward, planarCollision);
        float minDot = Mathf.Cos(maxFrontalAngle * Mathf.Deg2Rad);
        return includeFrontCollisions && dot >= minDot;
    }

    void TriggerSpin(Vector3 collisionDirection, float force)
    {
        if (crashing) return;
        if (_isDrifting) return;
        float spinDirection = Mathf.Sign(Vector3.Dot(transform.right, collisionDirection));
        rb.angularVelocity = Vector3.zero;
        rb.AddTorque(transform.up * spinDirection * spinTorqueForce * (force / spinMinForce), ForceMode.VelocityChange);
        rb.AddForce(-transform.forward * 5f, ForceMode.VelocityChange);
        OnCollision?.Invoke(0.5f, collisionDirection);
    }

    bool IsLateralCollision(Vector3 collisionDirection)
    {
        Vector3 planarForward = Vector3.ProjectOnPlane(transform.forward, cachedSurfaceNormal);
        Vector3 planarCollision = Vector3.ProjectOnPlane(collisionDirection, cachedSurfaceNormal);
        if (planarCollision.sqrMagnitude < 0.0001f) return false;
        planarForward.Normalize();
        planarCollision.Normalize();
        float dot = Vector3.Dot(planarForward, planarCollision);
        float minDot = Mathf.Cos(lateralMinAngle * Mathf.Deg2Rad);
        return Mathf.Abs(dot) < minDot;
    }

    void ResetCrashCam() => OnCrash?.Invoke(false);
    #endregion

    bool isCameraman;

    #region Respawn
    private void HandleRespawnSystem()
    {
        if (!isGrounded)
        {
            _airTimer += Time.fixedDeltaTime;
            if (_airTimer >= airTimeThreshold && !_isRespawning && isCameraman) ExecuteRespawn();
            if (_airTimer >= airTimeThreshold && !_isRespawning && !isCameraman) GameOver();
        }
    }

    Vector3 respawnPosition = new Vector3(0,0,0);
    Quaternion respawnRotation = new Quaternion(0f,0f,0f, 0f);
    bool respawnPositionManualSeted = false;

    public void SetRespawnPosition(Vector3 newPos, Quaternion newRot)
    {
        respawnPosition = newPos;
        respawnRotation = newRot;
        respawnPositionManualSeted = true;
    }

    bool IsAreaSafe() => cachedGroundedCount >= 4 && Vector3.Dot(cachedSurfaceNormal, Vector3.up) > 0.75f;

    private void ExecuteRespawn()
    {
        _isRespawning = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        if(!respawnPositionManualSeted){
            transform.position = _lastSafePosition;
            Transform target = GetRespawnTarget();
            //deformer?.RestoreMesh();
           // if(damageCar != null) damageCar.ResetDamage();
            //Limpa o visual de damaged aqui
            if (target != null)
            {
                Vector3 direction = (target.position - _lastSafePosition).normalized;
                Vector3 planarDir = Vector3.ProjectOnPlane(direction, cachedSurfaceNormal).normalized;
                if (planarDir.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(planarDir, cachedSurfaceNormal);
                else transform.rotation = Quaternion.Euler(new Vector3(0f, direction.y, 0f));
            }
            else transform.rotation = Quaternion.Euler(Vector3.zero);
        }
        else
        {
            transform.position = respawnPosition;
            transform.rotation = respawnRotation;
        }
        if(!AIControlled) { }
        else aiDriver.SetRecovering();
        Invoke(nameof(ResetRespawnFlag), 1f);
    }

    private void ResetRespawnFlag()
    {
        _isRespawning = false;
        _airTimer = 0;
    }

    private Transform GetRespawnTarget()
    {
        if (racerStatus == null) racerStatus = GetComponent<RacerStatus>();
        if(racerStatus.waypoints.Count == 0) return null;
        int index = (indexAtSavePoint + 15) % racerStatus.waypoints.Count;
        return racerStatus.waypoints[index];
    }

    private Vector3 GetCurrentPoint()
    {
        if (racerStatus == null) racerStatus = GetComponent<RacerStatus>();
        int index = racerStatus.currentWaypointIndex;
        indexAtSavePoint = index;
        Vector3 point = new Vector3(racerStatus.waypoints[index].position.x, (racerStatus.waypoints[index].position.y + racerStatus.waypoints[index].up.y * 1.25f), racerStatus.waypoints[index].position.z);
        return point;
    }
    #endregion

    #region Nitro
    public void ActivateTurbo()
    {
        if (NOS_amount > 0 && !_isTurboActive && Time.time >= _nextTurboTime)
        {
            NOS_amount--;
            _isTurboActive = true;
            turboTimer = 0f;
            rb.AddForce(transform.forward * turboInitialImpulse, ForceMode.VelocityChange);
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

    public void GainNOS(int qnt)
    {
        NOS_amount += qnt;
        Mathf.Clamp(NOS_amount, 0, max_NOS_amount);
    }
    #endregion

    #region Status Checking
    private void GroundCheck()
    {
        int tempGroundedWheels = 0;
        for (int i = 0; i < wheelsGrounded.Length; i++) tempGroundedWheels += wheelsGrounded[i];
        isGrounded = (tempGroundedWheels >= MIN_WHEELS_TO_CONSIDERE_GROUNDED);
        if (isGrounded) _airTimer = 0;
        if (isGrounded && IsAreaSafe())
        {
            _lastSafePosition = GetCurrentPoint();
            if (isHover) _lastSafePosition += transform.up * 1f;
            _lastSafeRotation = transform.rotation;
            _saveTimer = 0;
        }
        if (!wasGrounded && isGrounded)
        {

            Vector3 localVel = transform.InverseTransformDirection(rb.linearVelocity);

           // if (localVel.y < -5f)
           // {
                //localVel.y *= 0.35f;
                //rb.linearVelocity = transform.TransformDirection(localVel);
           // }

            OnLand?.Invoke(airTime);
            if (_currentHandbrakeInput)
            {
                TryStartDrift();
            }
            airTime = 0f;
        }
        else if (!isGrounded) airTime += Time.fixedDeltaTime;
        if (wasGrounded && !isGrounded) OnJump?.Invoke(0f);
        wasGrounded = isGrounded;

        if (isGrounded)
        {
            airborneTimer = 0f;
        }
        else
        {
            airborneTimer += Time.fixedDeltaTime;
        }
    }

    private void CalculateCarVelocity()
    {
        currentCarLocalVelocity = transform.InverseTransformDirection(rb.linearVelocity);
        carVelocityRatio = currentCarLocalVelocity.z / maxSpeed;
        speedKMH = currentCarLocalVelocity.z * 3.6f;
    }
    #endregion

    #region AI Status Changing Methods
    public void SetMaxSpeed(float value) => maxSpeed = value;
    #endregion

    #region Raycast System
    private void UpdateGroundSensors()
    {
        cachedSurfaceNormal = Vector3.zero;
        cachedAverageHeight = 0f;
        cachedGroundedCount = 0;
        Vector3 weightedNormalSum = Vector3.zero;
        float totalWeight = 0f;
        float castDistance = Mathf.Max(
            3f,
            rb.linearVelocity.magnitude * Time.fixedDeltaTime * 3f
        );
        Vector3 rayDir = -transform.up;
        for (int i = 0; i < rayPoints.Length; i++)
        {
            int hits = Physics.SphereCastNonAlloc(rayPoints[i].position, wheelRadius, rayDir, _raycastBuffer, castDistance, drivable);
            if (hits > 0)
            {
                groundStickTimer[i] = Mathf.Max(groundStickTimer[i], 0.1f);
                RaycastHit hit = _raycastBuffer[0];
                float minDist = hit.distance;
                

                RaycastHit bestHit = default;
                float bestScore = float.MinValue;

                for(int j = 0; j < hits; j++)
                {
                    hit = _raycastBuffer[j];

                    Vector3 suspensionDir = -rayDir;

                    float alignment =
                        Vector3.Dot(hit.normal, suspensionDir);

                    float score =
                        alignment * 2f
                        - hit.distance * 0.25f;

                    if(score > bestScore)
                    {
                        bestScore = score;
                        bestHit = hit;
                    }
                }

                if(bestScore > -999f)
                {
                    hit = bestHit;
                }
                else
                {
                    groundSensors[i].hit = false;
                    continue;
                }
                hit = bestHit;
                float compression = Mathf.Clamp01((restLenght - hit.distance) / springTravel);
                float weight = Mathf.Pow(compression, 2) + 0.01f;
                weightedNormalSum += hit.normal * weight;
                totalWeight += weight;
                groundSensors[i].hit = true;
                if (smoothedNormals[i] == Vector3.zero) smoothedNormals[i] = hit.normal;
                float smoothing = 1f - Mathf.Exp(-12f * Time.fixedDeltaTime);
                smoothedNormals[i] = Vector3.Slerp(smoothedNormals[i], hit.normal, smoothing);
                hit.normal = smoothedNormals[i];
                groundSensors[i].hitInfo = hit;
                cachedSurfaceNormal += hit.normal;
                cachedAverageHeight += hit.distance;
                cachedGroundedCount++;
            }
            else
            {
                groundSensors[i].hit = false;
                smoothedNormals[i] = Vector3.Lerp(smoothedNormals[i], transform.up, 0.1f);
                groundStickTimer[i] -= Time.fixedDeltaTime;
                groundSensors[i].hit = false;
            }
        }
        if (totalWeight > 0)
        {
            Vector3 targetNormal = weightedNormalSum / totalWeight;
            float smoothing = 1f - Mathf.Exp(-15f * Time.fixedDeltaTime);
            cachedSurfaceNormal = Vector3.Slerp(cachedSurfaceNormal, targetNormal.normalized, smoothing);
        }
        else cachedSurfaceNormal = Vector3.Slerp(cachedSurfaceNormal, transform.up, 5f * Time.fixedDeltaTime);
        if (cachedGroundedCount > 0)
        {
            cachedSurfaceNormal /= cachedGroundedCount;
            cachedSurfaceNormal.Normalize();
            cachedAverageHeight /= cachedGroundedCount;
        }
        else
    {
        cachedSurfaceNormal = Vector3.Slerp(
            cachedSurfaceNormal,
            lastGroundNormal,
            5f * Time.fixedDeltaTime
        );

        if (cachedGroundedCount > 0)
        {
            lastGroundNormal = cachedSurfaceNormal;
        }
    }
    }
    #endregion
}

public enum CarType { classic, hover }