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
    private static int MIN_WHEELS_TO_CONSIDERE_GROUNDED = 1;
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
    private RaycastHit[] _raycastBuffer = new RaycastHit[1];
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
    private Vector3 _currentCarUp = Vector3.up;
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
    [SerializeField] private int currentGear = 1;
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
    #endregion

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
                ApplyDownforce();
            }
            AlignToTrack();
            ApplySuspension();
            GroundCheck();
            HandleMovement();
            CalculateCarVelocity();
            Visuals();
            UpdateBodyTilt();
            if (_isDrifting) ApplyDriftForces();
            if (_isDrifting) LimitDriftAngle();
            if (_isTurboActive) ApplyTurboPhysics();
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
        if (currentGear < totalGears && engineRPM >= upshiftRPM) targetGear = currentGear + 1;
        else if (currentGear > 1 && engineRPM <= downshiftRPM) targetGear = currentGear - 1;
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
            OnUpGear.Invoke();
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
        rb.linearVelocity = transform.TransformDirection(localVel);
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
        if (Mathf.Abs(steerInput) < 0.1f && isGrounded && !_isDrifting)
        {
            Vector3 localAngularVel = transform.InverseTransformDirection(rb.angularVelocity);
            localAngularVel.y *= 0.9f;
            rb.angularVelocity = transform.TransformDirection(localAngularVel);
        }
        Vector3 turnAxis = cachedSurfaceNormal;
        if(carType == CarType.classic && !_isDrifting)
            rb.AddTorque(turnAxis * steerPower * steerInput * speedFactor * accelerationSteerBoost, ForceMode.VelocityChange);
        else
            rb.AddTorque(turnAxis * steerPower * steerInput * speedFactor * accelerationSteerBoost, ForceMode.Acceleration);
    }

    private void SidewaysDrag()
    {
        Vector3 planarVelocity = Vector3.ProjectOnPlane(rb.linearVelocity, cachedSurfaceNormal);
        Vector3 lateralDir = Vector3.Cross(cachedSurfaceNormal, transform.forward).normalized;
        float lateralSpeed = Vector3.Dot(planarVelocity, lateralDir);
        float dragForceAmount = -lateralSpeed * dragCoefficient;
        Vector3 dragForce = lateralDir * dragForceAmount;
        rb.AddForce(dragForce, ForceMode.Acceleration);
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
        
        // FORÇA DE STICK: puxa o carro PARA BAIXO quando a roda está no chão
        // Isso elimina os pulos completamente
        float stickForce = 0f;
        if (compression > -0.1f) // Roda no chão
        {
            // Quanto mais rápida a velocidade vertical para CIMA, mais stick force
            float upwardSpeed = Mathf.Max(0, springVelocity);
            stickForce = -upwardSpeed * rb.mass * 3f;
            stickForce = Mathf.Clamp(stickForce, -rb.mass * 10f, 0f);
        }
        
        netForce += stickForce;
        
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
        if(Mathf.Abs(_currentThrottleInput ) > 0.1f) _rearWheelRotationAccumulator += tireRorationSpeed * _currentThrottleInput * Time.deltaTime;
        else _rearWheelRotationAccumulator += tireRorationSpeed * carVelocityRatio * Time.deltaTime;
        _frontWheelRotationAccumulator += tireRorationSpeed * carVelocityRatio * Time.deltaTime;
        for (int i = 0; i < tires.Length; i++)
        {
            if (tires[i] == null) continue;
            if (i >= 2) tires[i].transform.localRotation = _currentBaseWheelRot * Quaternion.Euler(_rearWheelRotationAccumulator, 0, 0);
            else
            {
                tires[i].transform.localRotation = _currentBaseWheelRot * Quaternion.Euler(_frontWheelRotationAccumulator, 0, 0);
                if (frontTiresParent[i] != null) frontTiresParent[i].transform.localRotation = _currentBaseWheelRot * Quaternion.Euler(0, steeringAngle, 0);
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
        _currentBodyPitch = Mathf.Lerp(_currentBodyPitch, _targetPitch, Time.fixedDeltaTime * currentResponseSpeed);
        _currentBodyRoll = Mathf.Lerp(_currentBodyRoll, _targetRoll, Time.fixedDeltaTime * currentResponseSpeed);
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
            Vector3 targetDir = isGrounded ? cachedSurfaceNormal : Vector3.up;
            _currentDownDir = Vector3.Slerp(_currentDownDir, targetDir, 10f * Time.fixedDeltaTime);
            rb.AddForce(-_currentDownDir * df, ForceMode.Force);
            float extraStick = rb.mass * Mathf.Lerp(2f, 10f, speedRatio);
            rb.AddForce(-_currentDownDir * extraStick, ForceMode.Force);
            return;
        }
        if (cachedGroundedCount == 0) return;
        float heightRatio = cachedAverageHeight / restLenght;
        if (heightRatio < minHeightThreshold) return;
        float speedFactor = useDynamicDownforce ? speed / (maxSpeed / 3.6f) : 1f;
        Vector3 downDir = isGrounded ? cachedSurfaceNormal : Vector3.up;
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
        Vector3 targetUp = predictedNormal;
        rb.AddForce(-targetUp * 50f, ForceMode.Acceleration);
        rb.angularVelocity = Vector3.ClampMagnitude(rb.angularVelocity, 8f);
    }

    private void ForceLeveling()
    {
        float inputFactor = Mathf.Clamp01(Mathf.Abs(_currentThrottleInput) + Mathf.Abs(_currentSteerInput));
        float assist = 1f - inputFactor;
        Vector3 targetUp = predictedNormal;
        Vector3 torque = Vector3.Cross(transform.up, targetUp);
        float angle = torque.magnitude;
        if (angle < 0.001f) return;
        float strength = airControlStrength * 0.1f;
        Vector3 finalTorque = torque.normalized * angle * strength * assist;
        rb.AddTorque(finalTorque, ForceMode.Acceleration);
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
    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject == this.gameObject) return;
        if (((1 << collision.gameObject.layer) & drivable) != 0) return;
        float upDot = Vector3.Dot(cachedSurfaceNormal, Vector3.up);
        float impactForce = collision.relativeVelocity.magnitude;
        float currentSpeed = rb.linearVelocity.magnitude;
        Vector3 dir = -collision.contacts[0].normal;
        bool isCrashableObject = ((1 << collision.gameObject.layer) & crashable) != 0;
        if(damageCar != null) damageCar.ApplyDamage(0.00625f);

        Vector3 collisionNormal = collision.contacts[0].normal;
        float speedMagnitude = collision.relativeVelocity.magnitude;

        Vector3 forceDirection = -collisionNormal * speedMagnitude; 

        deformer.Deform(collision.contacts[0].point, forceDirection);
        
        if (enableCollisionShake && impactForce >= minForceToShake && Time.time >= lastCollisionShakeTime + shakeCooldown)
        {
            float finalForce = Mathf.Clamp01(impactForce / 20f) * shakeIntensity;
            OnCollision?.Invoke(finalForce, dir);
            lastCollisionShakeTime = Time.time;
        }

        if (isCrashableObject && impactForce > crashImpactForce && IsFrontalCollision(dir) && !_isTurboActive && (ShouldAvoidCrashForSlowPlayers() == false) && upDot > 0.6f)
        {
            if (racerStatus.isPlayer)
            {
                StartForceDrift();
                Invoke("EndForceDrift", 4f);
            }
            else
            {
                if (crashing) return;
                OnCrash?.Invoke(true);
                carCrash.TriggerCrash();
                Invoke("ResetCrashCam", carCrash.crashDuration);
            }
        }
        if (impactForce > spinMinForce && impactForce < spinMaxForceForCrash && currentSpeed > spinMinSpeed && IsLateralCollision(dir) && !_isTurboActive)
        {
            if(!racerStatus.isPlayer)
            {
                TriggerSpin(dir, impactForce);
            }
            else
            {
                float spinDirection = Mathf.Sign(Vector3.Dot(transform.right, dir));
                StartForceDrift();
                rb.AddTorque(transform.up * spinDirection * spinTorqueForce * ((impactForce / spinMinForce) / 2), ForceMode.VelocityChange);
                Invoke(nameof(EndForceDrift), 4f);
            }
            return;
        }
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

    #region Respawn
    private void HandleRespawnSystem()
    {
        if (!isGrounded)
        {
            _airTimer += Time.fixedDeltaTime;
            if (_airTimer >= airTimeThreshold && !_isRespawning) ExecuteRespawn();
        }
    }

    bool IsAreaSafe() => cachedGroundedCount >= 4 && Vector3.Dot(cachedSurfaceNormal, Vector3.up) > 0.75f;

    private void ExecuteRespawn()
    {
        _isRespawning = true;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        transform.position = _lastSafePosition;
        Transform target = GetRespawnTarget();
        deformer?.RestoreMesh();
        if(damageCar != null) damageCar.ResetDamage();
        //Limpa o visual de damaged aqui
        if (target != null)
        {
            Vector3 direction = (target.position - _lastSafePosition).normalized;
            Vector3 planarDir = Vector3.ProjectOnPlane(direction, cachedSurfaceNormal).normalized;
            if (planarDir.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(planarDir, cachedSurfaceNormal);
            else transform.rotation = _lastSafeRotation;
        }
        else transform.rotation = _lastSafeRotation;
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

    private void ApplyTurboPhysics()
    {
        if (_isTurboActive)
        {
            turboTimer += Time.deltaTime;
            float burst = (turboTimer < 0.8f) ? turboBurstForce : 1.0f;
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
        if (!wasGrounded && isGrounded && !isHover)
        {
            OnLand?.Invoke(airTime);
            if (_currentHandbrakeInput)
            {
                TryStartDrift();
            }
            airTime = 0f;
        }
        else if (!isGrounded) airTime += Time.deltaTime;
        if (wasGrounded && !isGrounded && !isHover) OnJump?.Invoke(0f);
        wasGrounded = isGrounded;
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
        float castDistance = isHover ? hoverDistance + 1.25f : restLenght;
        Vector3 rayDir = -transform.up;
        for (int i = 0; i < rayPoints.Length; i++)
        {
            int hits = Physics.SphereCastNonAlloc(rayPoints[i].position, wheelRadius, rayDir, _raycastBuffer, castDistance, drivable);
            if (hits > 0)
            {
                groundStickTimer[i] = Mathf.Max(groundStickTimer[i], 0.1f);
                RaycastHit hit = _raycastBuffer[0];
                float compression = Mathf.Clamp01((restLenght - hit.distance) / springTravel);
                float weight = Mathf.Pow(compression, 2) + 0.01f;
                weightedNormalSum += hit.normal * weight;
                totalWeight += weight;
                float minDist = hit.distance;
                for (int j = 1; j < hits; j++)
                {
                    if (_raycastBuffer[j].distance < minDist)
                    {
                        hit = _raycastBuffer[j];
                        minDist = hit.distance;
                    }
                }
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
                groundStickTimer[i] -= Time.deltaTime;
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
        else cachedSurfaceNormal = transform.up;
    }
    #endregion
}

public enum CarType { classic, hover }