using UnityEngine;
using System.Collections.Generic;

// Requer que o GameObject tenha um Rigidbody
[RequireComponent(typeof(Rigidbody))]
public class SCR_HoverPhysics : MonoBehaviour
{
    [SerializeField] float speedKMH;
    #region basic components
    [Header("Basic Components References")]
    [SerializeField] Rigidbody rb;
    [SerializeField] LayerMask drivable;
    [SerializeField] Transform accelerationPoint;
    [SerializeField] GameObject[] thrusters = new GameObject[4];
    [SerializeField] GameObject[] frontVanesParent = new GameObject[2];
    [SerializeField] Transform shipBody; // Referência ao corpo da nave para aplicar o tilt

    #endregion

    #region basic Setup

    [SerializeField] private static int MIN_THRUSTERS_TO_CONSIDERE_HOVERING = 2;

    #endregion

    #region suspension variables
    [Header("Suspension system")]
    [SerializeField] Transform[] rayPoints;
    [SerializeField] float springStiffness;
    [SerializeField] float restLenght;
    [SerializeField] float springTravel;
    [SerializeField] float thrusterRadius;
    [SerializeField] float damperStiffness;

    private int[] thrustersGrounded = new int[4];
    private bool isGrounded = false;

    [Header("Anti-Roll Bar")]
    [SerializeField] float antiRollForce = 5000f; // Força aplicada pela barra anti-roll
    private float[] thrusterHitDistances = new float[4]; // Distâncias armazenadas
    private float[] thrusterCompressions = new float[4]; // Compressões calculadas

    #endregion

    #region Ship Settings
    [Header("Ship Settings")]
    [SerializeField] float acceleration = 25f;
    [SerializeField] float maxSpeed = 100f;
    [SerializeField] float deceleration = 10f;
    [SerializeField] float steerStrenght = 15f;
    [SerializeField] AnimationCurve turningCurve;
    [SerializeField] float dragCoefficient = 1f;
    [SerializeField] float airControlStrength;

    #region Drift System
    [Header("Drift System Settings")]
    [SerializeField] private float maxDriftAngle = 45f; // Ângulo máximo de drift (graus)
    [SerializeField] private float driftEnterThreshold = 0.3f; // Velocidade mínima para entrar em drift (0-1)
    [SerializeField] private float driftExitThreshold = 0.1f; // Acelerador mínimo para manter drift
    [SerializeField] private float driftBoostForce = 50f; // Força do boost ao sair do drift
    [SerializeField] private float driftBoostDuration = 1f; // Duração do boost
    [SerializeField] private float driftSteerMultiplier = 1.5f; // Multiplicador de esterço durante drift
    [SerializeField] private float driftStability = 0.8f; // Estabilidade durante drift (0-1, mais baixo = mais escorregadio)
    [SerializeField] private float driftAccelerationMultiplier = 1.2f; // Multiplicador de aceleração durante drift
    [SerializeField] private float maxDriftBoostSpeed = 150f; // Velocidade máxima durante boost de drift

    // Estado do drift
    private bool _isDrifting = false;
    private float _currentDriftAngle = 0f;
    private float _driftBoostTimer = 0f;
    private bool _isDriftBoostActive = false;
    private float _originalDragCoefficient;
    private float _originalMaxSpeed;

    #endregion

    #region Public Getters - Drift State

    public bool IsDrifting() => _isDrifting;
    public float MaxSpeed() => maxSpeed;
    public float GetDriftAngle() => _currentDriftAngle;
    public float GetNormalizedDriftAngle() => Mathf.Clamp01(Mathf.Abs(_currentDriftAngle) / maxDriftAngle);
    public bool IsDriftBoostActive() => _isDriftBoostActive;
    public float GetDriftBoostRemainingTime() => _driftBoostTimer;

    // Para VFX/SFX saberem se deve mostrar efeitos de drift
    public bool ShouldShowDriftEffects() => _isDrifting && Mathf.Abs(_currentDriftAngle) > 10f;

    // Para VFX/SFX saberem a intensidade do drift (0-1)
    public float GetDriftIntensity() => Mathf.Clamp01(Mathf.Abs(_currentDriftAngle) / maxDriftAngle);

    // Para VFX/SFX saberem a direção do drift (-1 = esquerda, 1 = direita)
    public float GetDriftDirection() => Mathf.Sign(_currentDriftAngle);

    // Na região de Input Handling, adicione:
    public float GetThrottleInput() => _currentThrottleInput;
    public float GetBrakeInput() => _currentBrakeInput;
    public float GetSteerInput() => _currentSteerInput;
    public float GetCurrentSpeed() => speedKMH;

    #endregion

    [Header("Body Tilt Settings")]
    [SerializeField] float maxPitchAngle = 5f; // Tilt para frente/trás (aceleração/freio)
    [SerializeField] float maxRollAngle = 10f; // Tilt para os lados (direção)
    [SerializeField] float tiltResponseSpeed = 5f; // Velocidade de resposta do tilt
    [SerializeField] float tiltReturnSpeed = 3f; // Velocidade de retorno ao normal
    [SerializeField] float brakeTiltMultiplier = 1.5f; // Multiplicador do tilt ao frear
    [SerializeField] AnimationCurve speedTiltCurve; // Curva para ajustar o tilt baseado na velocidade

    [Header("Gravity/Ground Hugging")]
    [SerializeField] float gravityStrength = 9.81f; // Força de gravidade que puxa a nave
    [SerializeField] float surfaceAlignmentSpeed = 10f; // Velocidade de rotação para alinhar à nova superfície
    [SerializeField] float groundHugDistance = 1.5f; // Distância do raycast de busca de superfície

    private Vector3 _currentShipUp = Vector3.up; // O "Up" atual da nave (normal da superfície)

    private Vector3 currentShipLocalVelocity = Vector3.zero;
    private float shipVelocityRatio = 0;

    #region Visual variables

    [SerializeField] private float thrusterRotationSpeed = 3000f;
    [SerializeField] private float maxSteerAngle = 30f;

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

    // Valores para o visual da nave
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
    
    public void ActivateTurbo()
    {
        if (!_isTurboActive)
        {
            _isTurboActive = true;
            //_turboEndTime = Time.time + turboDuration;
        }
    }
    
    public void DeactivateTurbo()
    {
        _isTurboActive = false;
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
        
        // Se shipBody não foi atribuído, usar o próprio transform
        if(shipBody == null)
        {
            Debug.LogWarning("ShipBody não atribuído. Usando transform do GameObject.");
            shipBody = transform;
        }
    }

    public void InitializeStats(bool isAI)
    {
        if (isAI)
        {
            // Aplicando as proporções
            acceleration *= 4.5f;
            maxSpeed *= 6.0f;
            deceleration /= 8f;
        }
    }

    void Update()
    {
        UpdateDriftState();
        UpdateDriftBoost();
        CalculateDriftAngle();
    }

    void FixedUpdate()
    {
        if (isGrounded)
        {
            rb.useGravity = false;
            UpdateGravityDirection();
            ApplyAngularDamping();
        }
        else
        {
            rb.useGravity = true;
        }

        ApplySuspension();
        GroundCheck();

        HandleMovement();

        CalculateShipVelocity();

        Visuals();
        
        UpdateBodyTilt();

        if (_isDrifting)
        {
            ApplyDriftForces();
            LimitDriftAngle();
        }
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
        float currentAcceleration = acceleration;
        
        // Multiplicador de aceleração durante drift
        if (_isDrifting)
        {
            currentAcceleration *= driftAccelerationMultiplier;
        }
        
        // Multiplicador de aceleração durante boost
        if (_isDriftBoostActive)
        {
            currentAcceleration *= 2f; // Dobra a aceleração durante o boost
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
        
        // Multiplicador de direção durante drift
        // Se estiver virando contra a direção do drift, aumente a força de recuperação
        float steerInput = _currentSteerInput;
        if (_isDrifting && Mathf.Sign(steerInput) != Mathf.Sign(_currentDriftAngle))
        {
            steerPower *= 1.25f; // Recuperação mais rápida
        }
        
        // Fator que aumenta o poder de direção quando acelerando
        float accelerationSteerBoost = 1f + (_currentThrottleInput * 0.3f);
        
        // Usa velocidade absoluta para a curva
        float speedFactor = turningCurve.Evaluate(Mathf.Abs(shipVelocityRatio));
        
        rb.AddTorque(steerPower * _currentSteerInput * speedFactor * accelerationSteerBoost 
            * transform.up, ForceMode.Acceleration);
    }

    private void SidewaysDrag()
    {
        float currentSidewaysSpeed = currentShipLocalVelocity.x;
        float dragMagnitude = -currentSidewaysSpeed * dragCoefficient;
        
        // Durante drift, reduzimos o arrasto lateral para permitir deslizamento
        if (_isDrifting)
        {
            dragMagnitude *= driftStability;
        }
        // Reduz arrasto lateral quando acelerando/curvando normalmente
        else if (Mathf.Abs(_currentSteerInput) > 0.1f && _currentThrottleInput > 0.1f)
        {
            dragMagnitude *= 0.5f; // 50% menos arrasto
        }
        
        Vector3 dragForce = dragMagnitude * transform.right;
        rb.AddForceAtPosition(dragForce, rb.worldCenterOfMass, ForceMode.Acceleration);
    }

    #endregion

    #region Suspension

    void ApplySuspension()
    {
        for (int i = 0; i < rayPoints.Length; i++)
        {
            RaycastHit hit;
            float maxLenght = restLenght + springTravel;

            // O raycast deve sempre ir na direção down da nave (agora transform.up)
            if(Physics.Raycast(rayPoints[i].position, -transform.up, out hit, maxLenght + thrusterRadius, drivable))
            {
                thrustersGrounded[i] = 1;

                float currentSpringLenght = hit.distance - thrusterRadius;
                
                //Fórmula de compressão correta
                float springCompressionRatio = (restLenght - currentSpringLenght) / springTravel;

                // Usar transform.up (direção de subida da nave) para a velocidade vertical
                float springVelocity = Vector3.Dot(rb.GetPointVelocity(rayPoints[i].position), transform.up);
                float dampForce = damperStiffness * springVelocity;

                // Força de mola usa o Ratio
                float springForce = springStiffness * springCompressionRatio;

                float netForce = springForce - dampForce;

                // Aplicar força na direção UP da nave (transform.up)
                rb.AddForceAtPosition(netForce * transform.up, rayPoints[i].position);

                // visuals
                //Usar transform.up para posicionar o propulsor
                SetThrusterPosition(thrusters[i], hit.point + transform.up * thrusterRadius);
            }
            else
            {
                thrustersGrounded[i] = 0;
                
                // visuals (posicionar propulsor no ponto mais baixo se não houver contato)
                SetThrusterPosition(thrusters[i], rayPoints[i].position - transform.up * maxLenght); // Usar maxLenght
            }
        }
        
        // Aplica a gravidade real na direção oposta ao UP da pista (Ground Hugging)
        // Isso é crucial para manter a nave "grudada" em paredes/tetos
        rb.AddForce(-_currentShipUp * rb.mass * gravityStrength);
    }

    #endregion

    #region Visuals

    private void Visuals()
    {
        ThrusterVisuals();
    }

    private void ThrusterVisuals()
    {
        float steerigAngle = maxSteerAngle * _currentSteerInput;

        for(int i = 0; i < thrusters.Length; i++)
        {
            if(i < 2)
            {
                thrusters[i].transform.Rotate(Vector3.right, thrusterRotationSpeed * shipVelocityRatio * Time.deltaTime);

                frontVanesParent[i].transform.localEulerAngles = new Vector3(frontVanesParent[i].transform.localEulerAngles.x, steerigAngle, frontVanesParent[i].transform.localEulerAngles.z);
            }
            else
            {
                thrusters[i].transform.Rotate(Vector3.right, thrusterRotationSpeed * _currentThrottleInput * Time.deltaTime);
            }
        }
    }
    
    private void UpdateBodyTilt()
    {
        if (shipBody == null) return;
        
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
        
        // Aplicar a rotação ao corpo da nave
        // Preservar a rotação Y (direção) original do corpo
        float currentYaw = shipBody.localEulerAngles.y;
        shipBody.localEulerAngles = new Vector3(_currentBodyPitch, currentYaw, _currentBodyRoll);
    }

    private void SetThrusterPosition(GameObject thruster, Vector3 targetPosition)
    {
        thruster.transform.position = targetPosition;
    }
    #endregion

    #region Physics - Stability

    private void ApplyAngularDamping()
    {
        // Amortecimento de Roll (Eixo Z Local) e Pitch (Eixo X Local)
        // O Yaw (Eixo Y Local) é tratado pelo Turn e SidewaysDrag, deve ser menos amortecido.
        
        // Converte a velocidade angular global para o espaço local da nave
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
        
        // Aplica o torque no espaço local da nave
        rb.AddRelativeTorque(controlTorque, ForceMode.Acceleration);

        // 2. Força de Nivelamento (Auto-Leveling)
        ForceLeveling();
        
        // 3. Puxão para baixo (Simulação de asas ou gravidade aumentada)
        // Isso garante que a nave não fique planando indefinidamente, acelerando o retorno ao chão.
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
        // Alinha o vetor 'up' da nave com o 'up' do mundo
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
        // Usamos rb.worldCenterOfMass como origem para garantir que estamos capturando a superfície onde o centro da nave está.
        if (Physics.Raycast(rb.worldCenterOfMass, -transform.up, out hit, groundHugDistance, drivable))
        {
            // Encontramos uma superfície (chão, parede ou teto)
            Vector3 targetUp = hit.normal;

            // Se a normal da superfície for muito diferente da direção atual da nave, 
            // rotacionamos para alinhar.

            // 2. Interpolação da Rotação da Nave
            Quaternion targetRotation = Quaternion.FromToRotation(transform.up, targetUp) * transform.rotation;
            
            // Usamos Slerp para rotação suave e LookRotation para manter a frente da nave (transform.forward)
            // e alinhar o up (targetUp).
            Quaternion smoothRotation = Quaternion.Slerp(rb.rotation, targetRotation, Time.fixedDeltaTime * surfaceAlignmentSpeed);

            rb.MoveRotation(smoothRotation);

            // 3. Atualiza a direção "UP" para a física de suspensão e gravidade
            _currentShipUp = targetUp;
        }
        else
        {
            // Se não detectarmos nada (pulo/ar), o Up volta a ser o Up do mundo (gravidade normal)
            _currentShipUp = Vector3.up;
            
            // A nave deve tentar se nivelar ao mundo (o que já fazemos em ForceLeveling/AirControl)
            // A rotação de Body (transform.rotation) não é mais forçada pelo raycast aqui.
        }
        
        // NOTA: É importante que o Rigidbody da nave não tenha a gravidade padrão do Unity marcada (Use Gravity = false)
        // para que apenas a AddForce(-_currentShipUp...) em ApplySuspension aplique a gravidade.
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
        // 1. A nave sair do chão
        // 2. A velocidade ficar muito baixa
        if (!isGrounded || rb.linearVelocity.magnitude < minDriftSpeed)
        {
            EndDrift(false); // Sai sem boost pois perdeu o controle/velocidade
        }
        
        // Note que removemos o "if (!hasThrottleInput)" daqui.
    }

    private void CalculateDriftAngle()
    {
        // Calcular o ângulo entre a direção da nave e a direção da velocidade
        Vector3 shipForward = transform.forward;
        Vector3 velocityDirection = rb.linearVelocity.normalized;
        
        // Ignorar componente vertical
        shipForward.y = 0;
        velocityDirection.y = 0;
        
        // Normalizar após remover componente Y
        shipForward.Normalize();
        velocityDirection.Normalize();
        
        // Verificar se temos vetores válidos
        if (shipForward.magnitude > 0.1f && velocityDirection.magnitude > 0.1f)
        {
            float rawAngle = Vector3.Angle(shipForward, velocityDirection);
            
            // Determinar direção do drift (positivo = drift para direita, negativo = para esquerda)
            float driftDirection = Mathf.Sign(Vector3.Cross(shipForward, velocityDirection).y);
            
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
        float forwardSpeed = currentShipLocalVelocity.z;
        if (forwardSpeed > 0 && driftIntensity > 0.3f)
        {
            float speedMaintainForce = driftIntensity * acceleration * 0.3f;
            rb.AddForce(transform.forward * speedMaintainForce, ForceMode.Acceleration);
        }
    }

    private void EndDrift(bool giveBoost)
    {
        _isDrifting = false;
        
        // Restaurar configurações normais
        dragCoefficient = _originalDragCoefficient;
        
        // Aplicar boost se merecido
        if (giveBoost && Mathf.Abs(_currentDriftAngle) > 20f) // Ângulo mínimo para ganhar boost
        {
            ApplyDriftBoost();
        }
    }

    private void ApplyDriftBoost()
    {
        _isDriftBoostActive = true;
        _driftBoostTimer = driftBoostDuration;
        
        // 1. Direção do Nariz
        Vector3 shipForward = transform.forward;
        
        // 2. Direção do Movimento Atual (Velocidade)
        Vector3 velocityDir = rb.linearVelocity.normalized;
        
        // 3. Mistura (Lerp): 70% frente da nave, 30% direção da velocidade
        // Isso "puxa" a nave para a frente, mas respeita a inércia atual
        Vector3 boostDirection = Vector3.Lerp(velocityDir, shipForward, 0.7f).normalized;
        
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

    #region Status checking

    private void GroundCheck()
    {
        int tempGroundedThrusters = 0;

        for (int i = 0; i < thrustersGrounded.Length; i++)
        {
            tempGroundedThrusters += thrustersGrounded[i];
        }

        isGrounded = (tempGroundedThrusters >= MIN_THRUSTERS_TO_CONSIDERE_HOVERING) ? true : false;
    }

    private void CalculateShipVelocity()
    {
        currentShipLocalVelocity = transform.InverseTransformDirection(rb.linearVelocity);
        shipVelocityRatio = currentShipLocalVelocity.z / maxSpeed;

        speedKMH = currentShipLocalVelocity.z * 3.6f;
    }
    
    #endregion

    #region AI status changing Methods

    public void SetMaxSpeed(float value)
    {
        maxSpeed = value;
    }

    #endregion
}