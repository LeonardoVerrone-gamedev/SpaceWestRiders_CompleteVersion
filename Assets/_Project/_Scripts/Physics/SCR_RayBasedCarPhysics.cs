using UnityEngine;
using System.Collections.Generic;
using UnityEditor.Experimental.GraphView;

// Requer que o GameObject tenha um Rigidbody
[RequireComponent(typeof(Rigidbody))]
public class SCR_RayBasedCarPhysics : MonoBehaviour
{
    public CarType carType;
    public bool canSwitchType = false;
    [SerializeField] float speedKMH;
    #region basic components
    [Header("Basic Components References")]
    [SerializeField] Rigidbody rb;
    [SerializeField] LayerMask drivable;
    [SerializeField] Transform accelerationPoint;
    [SerializeField] GameObject[] tires = new GameObject[4];
    [SerializeField] GameObject[] frontTiresParent = new GameObject[2];
    [SerializeField] Transform carBody; // Referência ao corpo do carro para aplicar o tilt
    AIRacingController aiDriver;

    #endregion

    [Header("Classic Grip Settings")]
    [HideInInspector] [SerializeField] float classicCornerGrip = 5.0f; // Força de "cola" no asfalto

    #region basic Setup

    [HideInInspector] [SerializeField] private static int MIN_WHEELS_TO_CONSIDERE_GROUNDED = 2;

    #endregion

    #region suspension variables
    [Header("Suspension system")]
    [SerializeField] Transform[] rayPoints;
    [HideInInspector][SerializeField] float springStiffness;
    [SerializeField] float restLenght;
    [SerializeField] float hoverDistance = 1.5f;
    [SerializeField] float springTravel;
    [HideInInspector][SerializeField] float wheelRadius;
    [HideInInspector][SerializeField] float hoverDamper = 8000f;
    [HideInInspector][SerializeField] float classicDamper = 3500f;

    [HideInInspector]private int[] wheelsGrounded = new int[4];
    [HideInInspector]private bool isGrounded = false;

    public bool AIControlled = false;

    #endregion

    #region downforce

    [Header("Hover Downforce (Aero)")]
    [SerializeField] private float hoverDownforceAmount = 2500f; // Força base
    [SerializeField] private float minHeightThreshold = 0.4f; // % da restLenght (ex: 40%)
    [SerializeField] private bool useDynamicDownforce = true;

    #endregion

    #region Car Settings
    [Header("Car Settings")]
    [SerializeField] float classicCarAcceleration = 25f;
    [SerializeField]float hoverCarAcceleration = 15f;
    [SerializeField] float acceleration = 25f;
    [SerializeField] float classicCarMaxSpeed = 100f;
    [SerializeField] float hoverCarMaxSpeed = 120f;
    [SerializeField] float maxSpeed = 100f;
    [SerializeField] float deceleration = 10f;
    [SerializeField] float steerStrenght = 15f;
    [HideInInspector][SerializeField] float dragCoefficient = 1f;
    [SerializeField] float classicCarDragCoefficient = 10f;
    [SerializeField] float hoverCarDragCoefficient = 2f;
    [SerializeField] float airControlStrength;

    [Header("Custom Curves for Balancing")]
    [HideInInspector][SerializeField] private AnimationCurve accelCurve;
    [HideInInspector][SerializeField] private AnimationCurve classicAccelCurve; // Linear e constante
    [HideInInspector][SerializeField] private AnimationCurve hoverAccelCurve;   // Lenta no início, forte no meio
    [HideInInspector][SerializeField] private AnimationCurve hoverCarTurningCurve; // Perde muito esterço em alta velocidade
    [SerializeField] private AnimationCurve classicCarTurningCurve;
    [HideInInspector][SerializeField] AnimationCurve turningCurve;

    #region Drift System
    [Header("Drift System Settings")]
    [HideInInspector][SerializeField] private float maxDriftAngle = 45f; // Ângulo máximo de drift (graus)
    [HideInInspector][SerializeField] private float driftEnterThreshold = 0.3f; // Velocidade mínima para entrar em drift (0-1)
    [SerializeField] private float driftBoostForce = 50f; // Força do boost ao sair do drift
    [SerializeField] private float driftBoostDuration = 1f; // Duração do boost
    [HideInInspector][SerializeField] private float driftStability = 0.8f; // Estabilidade durante drift (0-1, mais baixo = mais escorregadio)
    [HideInInspector][SerializeField] private float driftAccelerationMultiplier = 1.2f; // Multiplicador de aceleração durante drift
    [HideInInspector][SerializeField] private float maxDriftBoostSpeed = 150f; // Velocidade máxima durante boost de drift
    [HideInInspector][SerializeField] float speedMaintainForceMultiplier = .75f; //o quanto mantem a velocidade frontal
    [HideInInspector][SerializeField] private float driftSelfSteerStrength = 5f; // Força do auto-alinhamento
    [HideInInspector][SerializeField] private float driftCounterSteerLimit = 0.5f; // O quanto o carro pode "ajudar" sem tirar o controle do player
    [HideInInspector][SerializeField] private float dragRestoreDuration = 0.5f; // Tempo para recuperar o grip total
    private float _dragRestoreTimer = 0f;
    private bool _isRestoringDrag = false;

    // Estado do drift
    private bool _isDrifting = false;
    private float _currentDriftAngle = 0f;
    private float _driftBoostTimer = 0f;
    private bool _isDriftBoostActive = false;
    private float _originalDragCoefficient;
    private float _originalMaxSpeed;

    #endregion

    #region Public Getters - Drift State

    [HideInInspector]public bool IsDrifting() => _isDrifting;
    [HideInInspector]public float OriginalMaxSpeed() => classicCarMaxSpeed;
    [HideInInspector]public float GetDriftAngle() => _currentDriftAngle;
    [HideInInspector]public float GetNormalizedDriftAngle() => Mathf.Clamp01(Mathf.Abs(_currentDriftAngle) / maxDriftAngle);
    [HideInInspector]public bool IsDriftBoostActive() => _isDriftBoostActive;
    [HideInInspector]public float GetDriftBoostRemainingTime() => _driftBoostTimer;

    // Para VFX/SFX saberem se deve mostrar efeitos de drift
    [HideInInspector]public bool ShouldShowDriftEffects() => _isDrifting && Mathf.Abs(_currentDriftAngle) > 10f;

    // Para VFX/SFX saberem a intensidade do drift (0-1)
    [HideInInspector]public float GetDriftIntensity() => Mathf.Clamp01(Mathf.Abs(_currentDriftAngle) / maxDriftAngle);

    // Para VFX/SFX saberem a direção do drift (-1 = esquerda, 1 = direita)
    [HideInInspector]public float GetDriftDirection() => Mathf.Sign(_currentDriftAngle);

    // Na região de Input Handling, adicione:
    [HideInInspector]public float GetThrottleInput() => _currentThrottleInput;
    [HideInInspector]public float GetBrakeInput() => _currentBrakeInput;
    [HideInInspector]public float GetSteerInput() => _currentSteerInput;
    [HideInInspector]public float GetCurrentSpeed() => speedKMH;

    [HideInInspector]public bool IsGrounded => isGrounded;

    #endregion

    [Header("Body Tilt Settings")]
    [HideInInspector][SerializeField] float maxPitchAngle = 5f; // Tilt para frente/trás (aceleração/freio)
    [HideInInspector][SerializeField] float maxRollAngle = 10f; // Tilt para os lados (direção)
    [HideInInspector][SerializeField] float tiltResponseSpeed = 5f; // Velocidade de resposta do tilt
    [HideInInspector][SerializeField] float tiltReturnSpeed = 3f; // Velocidade de retorno ao normal
    [HideInInspector][SerializeField] float brakeTiltMultiplier = 1.5f; // Multiplicador do tilt ao frear
    [HideInInspector][SerializeField] AnimationCurve speedTiltCurve; // Curva para ajustar o tilt baseado na velocidade
    
    [Header("Gravity/Ground Hugging")]
    [HideInInspector][SerializeField] float gravityStrength = 9.81f; // Força de gravidade que puxa o carro
    [HideInInspector][SerializeField] float surfaceAlignmentSpeed = 10f; // Velocidade de rotação para alinhar à nova superfície
    [HideInInspector][SerializeField] float groundHugDistance = 1.5f; // Distância do raycast de busca de superfície
    [SerializeField] private float downforceAmount = 500f;
    public float extraGripModifier = 1.0f;
    private Vector3 _currentCarUp = Vector3.up; // O "Up" atual do carro (normal da superfície)

    private Vector3 currentCarLocalVelocity = Vector3.zero;
    private float carVelocityRatio = 0;

    #region Visual variables

    [HideInInspector][SerializeField] private float tireRorationSpeed = 3000f;
    [HideInInspector][SerializeField] private float maxSteerAngle = 30f;

    [Header("Hover Visual Transformation")]
    [HideInInspector][SerializeField] float transitionSpeed = 5f;
    // Alterado para a rotação desejada (0, 180, 90)
    [HideInInspector][SerializeField] Vector3 wheelHoverRotation = new Vector3(0, 180, 90); 
    [HideInInspector][SerializeField] Vector3 wheelClassicRotation = new Vector3(0, 180, 0); // Padrão clássico
    private float _hoverTransitionAlpha = 0f; // 0 = Classic, 1 = Hover

    [Header("Respawn System")]
    [HideInInspector][SerializeField] private float savePositionInterval = 2f;
    [HideInInspector][SerializeField] private float airTimeThreshold = 5f;
    [HideInInspector][SerializeField] private float respawnBoostIntensity = 1.5f; // Multiplicador de força no respawn
    
    [Header("Turbo & Stamina Settings")]
    [SerializeField] private float maxStamina = 100f;
    [SerializeField] private float currentStamina = 100f;
    [SerializeField] private float staminaConsumptionRate = 30f; // Quanto gasta por segundo
    [SerializeField] private float staminaRegenRate = 10f;       // Quanto recupera por segundo
    [SerializeField] private float turboInitialImpulse = 15f;    // O "X" do impulso inicial
    [SerializeField] private float turboMaxSpeedMultiplier = 2.0f;
    [SerializeField] private float turboAccelMultiplier = 2.0f;

    [Header("Turbo Cooldown Settings")]
    [SerializeField] private float _turboCooldownTime = 3; // Tempo de espera entre usos
    private float _nextTurboTime = 0f; // Marca quando o turbo poderá ser usado novamente

    [SerializeField]private bool _isTurboRequestActive = false; // Se o jogador está segurando o botão
    private Vector3 _lastSafePosition;
    private Quaternion _lastSafeRotation;
    private float _saveTimer;
    private float _airTimer;
    private bool _isRespawning = false;

    public float rubberBandingAccelerationMultiplier = 1f;

    #endregion

    #endregion
    #region Internal State

    private float _currentSteerInput; // Input de esterço (-1 a 1)
    private float _currentThrottleInput; // Input de aceleração (0 a 1)
    private float _currentBrakeInput; // Input de freio (0 a 1)
    private bool _currentHandbrakeInput; // Input de freio de mão (true/false)
    private float _driftExitTime = 0f;
    private bool _isTurboActive = false;
    private float _turboEndTime = 0f;
    private float _currentSteerAngle; // Ângulo de esterço atual interpolado
    private float _speedKPH; // Velocidade atual em Km/h
    private float _speedRatio; // Velocidade atual como razão de 0 a 1 (em relação a topSpeed)

    // Valores para o visual do carro
    private Vector3 _meshLocalEulerAngles;
    private float _currentRoll;
    private float _currentPitch;
    
    // Variáveis de tilt
    private float _targetPitch = 0f;
    private float _targetRoll = 0f;
    private float _currentBodyPitch = 0f;
    private float _currentBodyRoll = 0f;

    #endregion

    // --- Métodos Unity ---

    #region IA multipliers
    private float accelerationMultiplier;
    private float maxSpeedMultiplier;
    private float decelerationDecreaser;
    #endregion

    #region Unity Events
    
    
    #endregion

    // --- Gerenciamento de Inputs (Getters) ---

    #region Input Handling

    public void SetSteering(float input) => _currentSteerInput = input;
    public void SetThrottle(float input) => _currentThrottleInput = input;
    public void SetBrake(float input) => _currentBrakeInput = input;
    public void SetHandbrake(bool input) 
    {
        // Se apertou agora e está no chão, tenta começar
        if (input && !_currentHandbrakeInput && isGrounded)
        {
            TryStartDrift();
        }
        // Se soltou o botão e estava em drift, encerra
        else if (!input && _isDrifting)
        {
            EndDrift(true); // Finaliza com boost
        }
        
        _currentHandbrakeInput = input;
    }

    #endregion

    // --- Métodos de Física Central ---

    #region Unity Lifecycle
    void Awake()
    {
        _originalDragCoefficient = dragCoefficient;
        _originalMaxSpeed = maxSpeed;
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

        currentStamina = maxStamina;

        rb.useGravity = false;

        SwitchToMode(carType);
    }

    public void SetAI(bool isAI, float accMult, float speedMult, float decelDivisor, AIRacingController driver)
    {
        AIControlled = isAI;
        if(isAI){
            accelerationMultiplier = accMult;
            maxSpeedMultiplier = speedMult;
            decelerationDecreaser = decelDivisor;
            aiDriver = driver;
        }

        SwitchToMode(carType);
    }
    

    void Update()
    {
        HandleStamina();
        UpdateDriftState();
        UpdateDriftBoost();
        CalculateDriftAngle();
        UpdateDragRestoration();
    }

    void FixedUpdate()
    {
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

        HandleRespawnSystem();
    }

    #endregion

    #region transition between car modes

    public void ToggleVehicleMode()
    {
        if(!canSwitchType){return;}
        // Lógica de alternância (Grounded <-> Hover)
        if (carType == CarType.classic)
        {
            SwitchToMode(CarType.hover);
        }
        else if (carType == CarType.hover)
        {
            SwitchToMode(CarType.classic);
        }
    }

    public void SwitchToMode(CarType newMode)
    {
        carType = newMode;

        if (carType == CarType.hover)
        {
            // Ao entrar no modo hover, damos um pequeno "pulo" para cima
            // para o carro não tentar flutuar colado no chão
            dragCoefficient = hoverCarDragCoefficient;
            acceleration = hoverCarAcceleration;
            maxSpeed = hoverCarMaxSpeed;
            rb.AddForce(transform.up * 5f, ForceMode.VelocityChange);
        }
        else
        {
            acceleration = classicCarAcceleration;
            maxSpeed = classicCarMaxSpeed;
            dragCoefficient = classicCarDragCoefficient;
        }
        

       // if (AIControlled)
        //{
            // Aplicando as proporções
           // acceleration *= 5f;
           // maxSpeed *= 7.0f;
           // deceleration /= 8f;
       // }
        Debug.Log("Modo de condução alterado para: " + carType);
    }

    #endregion

    #region Movement

    private void HandleMovement()
    {
        if(isGrounded)
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
        }
        else
        {
            AirControl();
        }

        Turn();

    }

    private void Acceleration()
    {

        float effectiveMaxSpeed = maxSpeed;
        if (_isTurboActive) effectiveMaxSpeed *= turboMaxSpeedMultiplier;

        if (currentCarLocalVelocity.z >= effectiveMaxSpeed) return;

        float currentAcceleration = acceleration;
    
        // Calcula o percentual da velocidade atual (0 a 1)
        float speedPercentage = Mathf.Clamp01(currentCarLocalVelocity.z / maxSpeed);

        accelCurve = carType == CarType.hover ? hoverAccelCurve : classicAccelCurve;

        currentAcceleration *= accelCurve.Evaluate(speedPercentage);
        
        // Multiplicador de aceleração durante drift
        if (_isDrifting)
        {
            currentAcceleration *= driftAccelerationMultiplier;
        }

        if (_isTurboActive)
        {
            currentAcceleration *= turboAccelMultiplier;
        }
        
        // Multiplicador de aceleração durante boost
        if (_isDriftBoostActive)
        {
            currentAcceleration *= 2f; // Dobra a aceleração durante o boost
        }

        if (AIControlled)
        {
            currentAcceleration *= rubberBandingAccelerationMultiplier;
        }
        
        rb.AddForceAtPosition(currentAcceleration * _currentThrottleInput * transform.forward, accelerationPoint.position, ForceMode.Acceleration);
    }

    private void Deaceleration()
    {
        rb.AddForceAtPosition(deceleration * _currentThrottleInput * transform.forward, accelerationPoint.position, ForceMode.Acceleration);
    }

    private void Turn()
    {
        float TurnPowerMultiplier = isGrounded ? 1 : 0.5f;
        float steerPower = steerStrenght * TurnPowerMultiplier;
        float steerInput = _currentSteerInput;

        // --- LÓGICA DE SELF-STEERING (DENTRO DO DRIFT) ---
        if (_isDrifting && isGrounded)
        {
            // Se o jogador NÃO está dando input de direção (analogico solto ou neutro)
            if (Mathf.Abs(steerInput) < 0.1f)
            {
                // Calcula um torque suave oposto ao ângulo do drift para alinhar o nariz com a velocidade
                // Se o drift angle é positivo (cauda para a esquerda), precisamos de torque negativo para alinhar
                float selfSteerFactor = -(_currentDriftAngle / maxDriftAngle);
                steerInput = selfSteerFactor * driftSelfSteerStrength * Time.fixedDeltaTime;
                
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
        turningCurve = carType == CarType.classic ? classicCarTurningCurve : hoverCarTurningCurve;
        float speedFactor = turningCurve.Evaluate(Mathf.Abs(carVelocityRatio));

        if (Mathf.Abs(steerInput) < 0.1f && isGrounded && !_isDrifting)
        {
            // Reduz drasticamente a rotação quando solta o volante (efeito de centralização)
            Vector3 localAngularVel = transform.InverseTransformDirection(rb.angularVelocity);
            localAngularVel.y *= 0.9f; // Amortece o giro no eixo Y
            rb.angularVelocity = transform.TransformDirection(localAngularVel);
        }
        
        // Aplica o torque final (usando o steerInput processado pelo self-steering se necessário)
        rb.AddTorque(steerPower * steerInput * speedFactor * accelerationSteerBoost 
            * transform.up, ForceMode.Acceleration);
    }

    private void SidewaysDrag()
    {
        float currentSidewaysSpeed = currentCarLocalVelocity.x;
        
        // 1. Mantemos um drag base para não virar saboneteira
        float dragMagnitude = -currentSidewaysSpeed * dragCoefficient;
        
        // 2. Aplicamos o "Grip Assistido" para o Classic
        if (carType == CarType.classic && isGrounded && Mathf.Abs(_currentSteerInput) > 0.1f)
        {
            // Esta força empurra o carro para dentro da curva baseado no input do volante
            // Ela anula parte da força centrífuga que joga o carro na parede
            Vector3 gripDir = transform.right * _currentSteerInput;
            float speedFactor = rb.linearVelocity.magnitude / maxSpeed;
            
            rb.AddForce(gripDir * classicCornerGrip * speedFactor, ForceMode.Acceleration);
        }

        Vector3 dragForce = dragMagnitude * transform.right;
        rb.AddForceAtPosition(dragForce, rb.worldCenterOfMass, ForceMode.Acceleration);
    }

    #endregion

    #region Suspension

    void ApplySuspension()
    {
        float _distance;
        float _damperStiffness;
        bool isHover = carType == CarType.hover;

        // Define valores baseados no tipo
        if (carType == CarType.classic) { 
            _distance = restLenght; 
            _damperStiffness = classicDamper; 
        } else { 
            _distance = hoverDistance; 
            _damperStiffness = hoverDamper; 
        }

        for (int i = 0; i < rayPoints.Length; i++)
        {
            RaycastHit hit;
            float maxLenght = _distance + springTravel;
            float maxVisualLenght = .125f + .15f;

            if (Physics.Raycast(rayPoints[i].position, -transform.up, out hit, maxLenght + wheelRadius, drivable))
            {
                wheelsGrounded[i] = 1;
                float currentSpringLenght = hit.distance - wheelRadius;
                float springCompressionRatio = (_distance - currentSpringLenght) / springTravel;
                float springVelocity = Vector3.Dot(rb.GetPointVelocity(rayPoints[i].position), transform.up);
                
                float dampForce = _damperStiffness * springVelocity;
                float springForce = springStiffness * springCompressionRatio;
                float netForce = springForce - dampForce;

                if (carType == CarType.classic)
                {
                    // Se estiver flutuando acima do limite visual
                    if (currentSpringLenght > maxVisualLenght)
                    {
                        // Força de "ímã" aumenta exponencialmente com a distância
                        // Multiplicamos por um valor alto (ex: 50) para garantir que o peso vença a mola
                        float distanceGap = currentSpringLenght - maxVisualLenght;
                        float suctionMultiplier = 50f; 
                        
                        netForce -= distanceGap * suctionMultiplier * rb.mass;
                    }
                    else if (currentSpringLenght < _distance * 0.5f) 
                    {
                        // Se o carro for "esmagado" contra o chão no loop por causa da força G, 
                        // aumentamos a resistência da mola para ele não atravessar o asfalto
                        netForce *= 1.5f; 
                    }
                }

                rb.AddForceAtPosition(netForce * transform.up, rayPoints[i].position);

                // --- LÓGICA VISUAL ---
                if (carType == CarType.hover) {
                    // No Hover, a roda fica travada na posição do RayPoint (sem seguir o terreno)
                    SetTirePosition(tires[i], rayPoints[i].position);
                } else {
                    // No Classic, a roda segue o chão
                    float visualSpringDistance = Mathf.Min(currentSpringLenght, maxVisualLenght);
                    Vector3 visualPos = rayPoints[i].position - transform.up * visualSpringDistance;
                    
                    SetTirePosition(tires[i], visualPos);
                }
            }
            else
            {
                wheelsGrounded[i] = 0;
                // No ar: se for hover, mantém no ponto; se for classic, desce tudo
                Vector3 airPos = (carType == CarType.hover) ? rayPoints[i].position : rayPoints[i].position - transform.up * maxVisualLenght;
                SetTirePosition(tires[i], airPos);
            }
        }
        
        rb.AddForce(-_currentCarUp * rb.mass * gravityStrength);
    }

    #endregion

    #region Visuals

    private void Visuals()
    {
        TireVisuals();
    }

    private void TireVisuals()
    {
        UpdateWheelTransformation();

        // Se estiver no modo Hover, ignoramos o resto do processamento visual (esterço e rolagem)
        if (carType == CarType.hover) return;

        float steeringAngle = maxSteerAngle * _currentSteerInput;

        for (int i = 0; i < tires.Length; i++)
        {
            if (tires[i] == null) continue;

            float rotationMultiplier = (i < 2 ? carVelocityRatio : _currentThrottleInput);

            // Rotação de rolagem (apenas no modo clássico)
            tires[i].transform.Rotate(Vector3.right, tireRorationSpeed * rotationMultiplier * Time.deltaTime, Space.Self);

            // Esterço (apenas no modo clássico)
            if (i < 2 && frontTiresParent[i] != null)
            {
                // Nota: Como o UpdateWheelTransformation agora controla o frontTiresParent, 
                // precisamos garantir que o esterço não sobrescreva o X e Z da transição.
                Vector3 currentRot = frontTiresParent[i].transform.localEulerAngles;
                frontTiresParent[i].transform.localEulerAngles = new Vector3(currentRot.x, steeringAngle, currentRot.z);
            }
        }
    }

    private void UpdateWheelTransformation()
    {
        float targetAlpha = (carType == CarType.hover) ? 1f : 0f;
        _hoverTransitionAlpha = Mathf.MoveTowards(_hoverTransitionAlpha, targetAlpha, Time.deltaTime * transitionSpeed);

        // Calculamos a rotação baseada no progresso da transição
        Quaternion targetRot = Quaternion.Euler(Vector3.Lerp(wheelClassicRotation, wheelHoverRotation, _hoverTransitionAlpha));

        for (int i = 0; i < tires.Length; i++)
        {
            if (tires[i] == null) continue;

            if (i < 2 && frontTiresParent[i] != null)
            {
                frontTiresParent[i].transform.localRotation = targetRot;
            }
            else
            {
                tires[i].transform.localRotation = targetRot;
            }
        }
    }
    
    private void UpdateBodyTilt()
    {
        if (carBody == null) return;
        
        // Calcular velocidade atual em magnitude
        float currentSpeed = rb.linearVelocity.magnitude;
        float speedFactor = speedTiltCurve.Evaluate(Mathf.Clamp01(currentSpeed / maxSpeed));

        float driftRollMultiplier = _isDrifting ? 2f : 1f;
        
        // Calcular tilt para frente/trás (pitch) baseado na aceleração/freio
        if (_currentThrottleInput > 0.1f)
        {
            // Acelerando - tilt para trás
            _targetPitch = -maxPitchAngle * _currentThrottleInput * speedFactor;
        }
        else if (_currentBrakeInput > 0.1f)
        {
            // Freando - tilt para frente (mais pronunciado)
            _targetPitch = maxPitchAngle * _currentBrakeInput * brakeTiltMultiplier * speedFactor;
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
        
        // Aplicar suavização (lerp) para transições suaves
        float currentResponseSpeed = (Mathf.Abs(_targetPitch) > Mathf.Abs(_currentBodyPitch) || 
                                     Mathf.Abs(_targetRoll) > Mathf.Abs(_currentBodyRoll)) ? 
                                     tiltResponseSpeed : tiltReturnSpeed;
        
        _currentBodyPitch = Mathf.Lerp(_currentBodyPitch, _targetPitch, Time.deltaTime * currentResponseSpeed);
        _currentBodyRoll = Mathf.Lerp(_currentBodyRoll, _targetRoll, Time.deltaTime * currentResponseSpeed);
        
        // Aplicar a rotação ao corpo do carro
        // Preservar a rotação Y (direção) original do corpo
        float currentYaw = carBody.localEulerAngles.y;
        carBody.localEulerAngles = new Vector3(_currentBodyPitch, currentYaw, _currentBodyRoll);
    }

    private void SetTirePosition(GameObject tire, Vector3 targetPosition)
    {
        tire.transform.position = targetPosition;
    }
    #endregion

    #region Physics - Stability

    public void ApplyDownforce()
    {
        if(carType == CarType.classic)
        {
            float finalDownforce = rb.linearVelocity.magnitude * downforceAmount * extraGripModifier;
            rb.AddForce(-transform.up * finalDownforce, ForceMode.Force);
        }

        if (carType == CarType.hover)
        {
            // Calculamos a altura média atual baseada nos sensores que atingiram o chão
            float currentHeightSum = 0;
            int groundedCount = 0;

            // Reutilizamos a lógica que você já tem para os raios
            for (int i = 0; i < rayPoints.Length; i++)
            {
                RaycastHit hit;
                if (Physics.Raycast(rayPoints[i].position, -rayPoints[i].up, out hit, restLenght + hoverDistance, drivable))
                {
                    currentHeightSum += hit.distance;
                    groundedCount++;
                }
            }

            if (groundedCount > 0)
            {
                float averageHeight = currentHeightSum / groundedCount;
                float heightRatio = averageHeight / restLenght;

                // MARGEM DE SEGURANÇA: Só aplica se não estiver "raspando" no chão
                if (heightRatio > minHeightThreshold)
                {
                    float forceMultiplier = useDynamicDownforce ? (rb.linearVelocity.magnitude / (maxSpeed / 3.6f)) : 1.0f;
                    
                    // Força aplicada no "Down" local para manter estabilidade em inclinações
                    Vector3 downforce = -transform.up * (hoverDownforceAmount * forceMultiplier);
                    
                    rb.AddForce(downforce, ForceMode.Force);
                }
            }
        }
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
        float pitchInput = _currentThrottleInput - _currentBrakeInput; 
        
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
        
        // 3. Puxão para baixo (Simulação de asas ou gravidade aumentada)
        // Isso garante que o carro não fique planando indefinidamente, acelerando o retorno ao chão.
        rb.AddForce(-Vector3.up * 50f, ForceMode.Acceleration); // Aumentei a força
    }

    private void ForceLeveling()
    {
        // Apenas nivelar se não houver input de controle aéreo substancial
        if (Mathf.Abs(_currentThrottleInput) > 0.1f || 
            Mathf.Abs(_currentBrakeInput) > 0.1f || 
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
        RaycastHit hit;
        
        // 1. Raycast central para detectar a superfície abaixo.
        // Usamos rb.worldCenterOfMass como origem para garantir que estamos capturando a superfície onde o centro do carro está.
        if (Physics.Raycast(rb.worldCenterOfMass, -transform.up, out hit, groundHugDistance, drivable))
        {
            // Encontramos uma superfície (chão, parede ou teto)
            Vector3 targetUp = hit.normal;

            // Se a normal da superfície for muito diferente da direção atual do carro, 
            // rotacionamos para alinhar.

            // 2. Interpolação da Rotação do Carro
            Quaternion targetRotation = Quaternion.FromToRotation(transform.up, targetUp) * transform.rotation;
            
            // Usamos Slerp para rotação suave e LookRotation para manter a frente do carro (transform.forward)
            // e alinhar o up (targetUp).
            Quaternion smoothRotation = Quaternion.Slerp(rb.rotation, targetRotation, Time.fixedDeltaTime * surfaceAlignmentSpeed);

            rb.MoveRotation(smoothRotation);

            // 3. Atualiza a direção "UP" para a física de suspensão e gravidade
            _currentCarUp = targetUp;
        }
        else
        {
            // Se não detectarmos nada (pulo/ar), o Up volta a ser o Up do mundo (gravidade normal)
            _currentCarUp = Vector3.up;
            
            // O carro deve tentar se nivelar ao mundo (o que já fazemos em ForceLeveling/AirControl)
            // A rotação de Body (transform.rotation) não é mais forçada pelo raycast aqui.
        }
        
        // NOTA: É importante que o Rigidbody do carro não tenha a gravidade padrão do Unity marcada (Use Gravity = false)
        // para que apenas a AddForce(-_currentCarUp...) em ApplySuspension aplique a gravidade.
    }

    void AlignToTrack()
    {
        Vector3 targetNormal = GetSurfaceNormal();
        
        // Calcula a rotação alvo baseada na normal da pista
        Quaternion targetRotation = Quaternion.FromToRotation(transform.up, targetNormal) * transform.rotation;

        // Para a IA, usamos um Slerp mais agressivo
        float alignmentForce = 12f;
        
        rb.MoveRotation(Quaternion.Slerp(transform.rotation, targetRotation, Time.fixedDeltaTime * alignmentForce));
    }

    private Vector3 GetSurfaceNormal()
    {
        Vector3 averageNormal = transform.up; // Fallback
        int hits = 0;
        Vector3 combinedNormal = Vector3.zero;

        // Definição de 3 raios extras: um bem à frente, um na esquerda e um na direita
        // Eles devem ser ligeiramente inclinados para fora (como um guarda-chuva)
        Vector3[] sensorOffsets = {
            transform.forward * 2.5f,           // Sensor frontal longo
            -transform.right * 1.5f,            // Sensor lateral esquerdo
            transform.right * 1.5f,             // Sensor lateral direito
            (transform.forward + transform.right) * 2f, // Diagonal
            (transform.forward - transform.right) * 2f  // Diagonal
        };

        foreach (Vector3 offset in sensorOffsets)
        {
            RaycastHit hit;
            // Atiramos o raio um pouco mais longo que a suspensão para "prever" o chão
            if (Physics.Raycast(transform.position + transform.up * 0.5f + offset, -transform.up * 2.5f, out hit, 5f, drivable))
            {
                combinedNormal += hit.normal;
                hits++;
            }
        }

        return hits > 0 ? combinedNormal.normalized : transform.up;
    }

    #endregion

    #region Physics - Drift System

    private void TryStartDrift()
    {
        // Só pode começar drift se estiver no chão
        if (!isGrounded) return;
        
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
    }

    private void UpdateDriftState()
    {
        if (!_isDrifting) return;
        
        // Calcular velocidade mínima de segurança para não driftar parado
        float minDriftSpeed = maxSpeed * driftEnterThreshold * 0.3f;
        
        // O drift agora SÓ termina automaticamente se:
        // 1. O carro sair do chão
        // 2. A velocidade ficar muito baixa
        if (!isGrounded || rb.linearVelocity.magnitude < minDriftSpeed)
        {
            EndDrift(false); // Sai sem boost pois perdeu o controle/velocidade
        }
        
        // Note que removemos o "if (!hasThrottleInput)" daqui.
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
        if (!isGrounded) return;
        
        // Calcular intensidade do drift (0 a 1)
        float driftIntensity = Mathf.Clamp01(Mathf.Abs(_currentDriftAngle) / maxDriftAngle);
        
        // Força centrífuga durante drift (proporcional ao ângulo)
        float centrifugalForce = rb.linearVelocity.magnitude * driftIntensity * 0.5f;
        Vector3 forceDirection = -transform.right * Mathf.Sign(_currentDriftAngle);
        rb.AddForce(forceDirection * centrifugalForce, ForceMode.Acceleration);
        
        // Reduzir arrasto frontal durante drift para manter velocidade
        float forwardSpeed = currentCarLocalVelocity.z;
        if (forwardSpeed > 0 && driftIntensity > 0.3f)
        {
            float speedMaintainForce = driftIntensity * acceleration * speedMaintainForceMultiplier;
            rb.AddForce(transform.forward * speedMaintainForce, ForceMode.Acceleration);
        }
    }

    private void EndDrift(bool giveBoost)
    {
        _isDrifting = false;
        
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

        // Interpola entre o drag de drift (estabilidade reduzida) e o original
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
        
        if (_driftBoostTimer <= 0)
        {
            EndDriftBoost();
        }
    }

    private void EndDriftBoost()
    {
        _isDriftBoostActive = false;
        maxSpeed = _originalMaxSpeed;
    }

    #endregion

    #region respawn 

    private void HandleRespawnSystem()
    {
        if (isGrounded)
        {
            _airTimer = 0; // Reseta o timer se estiver no chão
            _saveTimer += Time.fixedDeltaTime;

            // Salva a posição se o intervalo passou e o carro está estável (velocidade mínima)
            if (_saveTimer >= savePositionInterval && rb.linearVelocity.magnitude > 1f && IsAreaSafe())
            {
                _lastSafePosition = transform.position;
                // No modo Hover, garantimos que ele respawne um pouco acima do chão
                if (carType == CarType.hover) _lastSafePosition += Vector3.up * 1f;
                
                _lastSafeRotation = transform.rotation;
                _saveTimer = 0;
            }
        }
        else
        {
            _airTimer += Time.fixedDeltaTime;

            // Se passar do tempo limite no ar, executa o respawn
            if (_airTimer >= airTimeThreshold && !_isRespawning)
            {
                ExecuteRespawn();
            }
        }
    }

    bool IsAreaSafe() 
    {
        // Atira raios para as diagonais frontais para ver se há chão à frente
        Vector3 rightCheck = transform.position + (transform.right * 2f);
        Vector3 leftCheck = transform.position - (transform.right * 2f);
        
        bool hasGroundAhead = Physics.Raycast(transform.position + transform.forward * 3f, -transform.up, 5f, drivable);
        bool hasGroundRight = Physics.Raycast(rightCheck, -transform.up, 5f, drivable);
        bool hasGroundLeft = Physics.Raycast(leftCheck, -transform.up, 5f, drivable);

        return hasGroundAhead && hasGroundRight && hasGroundLeft;
    }

    private void ExecuteRespawn()
    {
        _isRespawning = true;
        
        // Zera as forças físicas para não respawnar com a inércia da queda
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Reposiciona o carro
        transform.position = _lastSafePosition;
        transform.rotation = _lastSafeRotation;

        // Aplica um boost de aceleração imediato para retomar a corrida
        if(!AIControlled){
            rb.AddForce(transform.forward * acceleration * respawnBoostIntensity, ForceMode.VelocityChange);
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

    #endregion

    #region Turbo

    public void ActivateTurbo() 
    {
        if (Time.time >= _nextTurboTime && currentStamina > 5f && !_isTurboActive) 
        {
            _isTurboRequestActive = true;
            
            // Aplica o impulso inicial "X"
            rb.AddForce(transform.forward * turboInitialImpulse, ForceMode.VelocityChange);
        }
    }

    public void DeactivateTurbo() 
    {
        if (_isTurboRequestActive) // Se estava ativo e ele soltou agora
        {
            _nextTurboTime = Time.time + _turboCooldownTime;
        }
        _isTurboRequestActive = false;
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
    }

    private void CalculateCarVelocity()
    {
        currentCarLocalVelocity = transform.InverseTransformDirection(rb.linearVelocity);
        carVelocityRatio = currentCarLocalVelocity.z / maxSpeed;

        speedKMH = currentCarLocalVelocity.z * 3.6f;
    }

    private void HandleStamina()
    {
        if (_isTurboRequestActive && currentStamina > 0)
        {
            _isTurboActive = true;
            currentStamina -= staminaConsumptionRate * Time.deltaTime;
            
            // Se acabar a stamina, desliga automaticamente
            if (currentStamina <= 0)
            {
                currentStamina = 0;
                _isTurboActive = false;
            }
        }
        else
        {
            _isTurboActive = false;
        }

        if (!_isTurboActive)
        {
            // Regenera se não estiver usando
            if (currentStamina < maxStamina)
            {
                currentStamina += staminaRegenRate * Time.deltaTime;
                currentStamina = Mathf.Min(currentStamina, maxStamina);
            }
        }
    }
    
    #endregion

    #region AI status changing Methods

    public void SetMaxSpeed(float value)
    {
        maxSpeed = value;
    }


    #endregion
}
public enum CarType {classic, hover};