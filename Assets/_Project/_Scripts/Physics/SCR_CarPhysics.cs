using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class WheelData
{
    public WheelCollider collider;
    public Transform visual;
    public bool isFrontWheel;
    public bool canSteer;
    public bool canDrive;
    
    [HideInInspector] public Vector3 originalVisualRotation;
    [HideInInspector] public bool isGrounded => collider != null && collider.isGrounded;
    [HideInInspector] public float currentCompression;
    [HideInInspector] public WheelHit wheelHit;
    [HideInInspector] public int wheelIndex;
    
    public enum WheelPosition { FrontLeft, FrontRight, RearLeft, RearRight }
    public WheelPosition position;
}

public class SCR_CarPhysics : MonoBehaviour
{
    #region Core Components
    [Header("Core Components")]
    [SerializeField] private Rigidbody _rb;
    [SerializeField] private Transform _centerOfMass;
    #endregion

    #region Wheel System
    [Header("Wheel System")]
    [SerializeField] private WheelData[] _wheels;
    
    private List<WheelData> _frontWheels = new List<WheelData>();
    private List<WheelData> _rearWheels = new List<WheelData>();
    private List<WheelData> _steerWheels = new List<WheelData>();
    private List<WheelData> _driveWheels = new List<WheelData>();
    #endregion

    #region Wheel Visual Rotation State
    private Dictionary<WheelData, float> _wheelRotationAccumulated = new Dictionary<WheelData, float>();
    #endregion

    #region Performance Parameters
    [Header("Performance Parameters")]
    public float acceleration = 7000f;
    public float brakingForce = 2505600f;
    public float maxSteeringAngle = 30f;
    public float topSpeed = 64f;
    public float reverseSpeed = 40f;
    public float naturalResistance = 300f;
    
    [Header("Steering Reduction")]
    [Range(0.1f, 1f)]
    public float steeringReductionAtMaxSpeed = 0.4f;
    #endregion

    #region Downforce System
    [Header("Downforce System")]
    public float downforce = 450f;
    public float maxDownforce = 50000f;
    public float additionalLoopDownforce = 400f;
    private bool _loopModeActive = false;
    #endregion

    #region Grip Settings - Sistema Antigo que Funciona
    [Header("Grip Settings (NFS Style)")]
    public float gripLateralHigh = 8f;
    public float gripForwardHigh = 6f;
    
    public float extremumSlipLateralHigh = 0.3f;
    public float extremumSlipForwardHigh = 0.15f;
    public float asymptoteSlipHigh = 0.5f;
    public float asymptoteValueHigh = 3f;
    public float gripStiffness = 1.2f;
    #endregion

    #region Suspension Settings
    [Header("Suspension Settings")]
    public float suspensionSpringRate = 45000f;
    public float suspensionDamper = 6000f;
    public float suspensionDistance = 0.08f;
    #endregion

    #region Stability Systems
    [Header("Stability Systems")]
    public float antiRollForce = 4500f;
    public float curveStabilization = 2.0f;
    #endregion

    #region Differential
    [Header("Differential")]
    [Range(0f, 1f)]
    public float differentialForce = 0.3f;
    #endregion

    #region Turbo System
    [Header("Turbo System")]
    public float turboTopSpeed = 80f;
    public float turboAcceleration = 11900f;
    public float maxTurboEnergy = 150f;
    public float turboConsumptionRate = 25f;
    public float turboRechargeRate = 15f;
    public ParticleSystem turboParticles;
    #endregion

    #region Drivetrain Configuration
    [Header("Drivetrain Configuration")]
    [Tooltip("0.8 = 80% tração traseira, 1 = 100% traseira, 0 = 100% dianteira")]
    [Range(0f, 1f)]
    public float driveDistribution = 0.8f; // 0.8 = 80% tração traseira
    
    [Tooltip(" 0 = sem direção nas rodas traseiras, 1 = 100% 4wd")]
    [Range(0f, 1f)]
    public float rearSteeringRatio = 0f; // 0 = sem direção nas rodas traseiras
    #endregion

    #region Controle Aereo
    [Header("Controle Aéreo - Mario Kart Style")]
    [Tooltip("Força de rotação no ar (estilo Mario Kart)")]
    public float forcaRotacaoAerea = 800f;

    [Tooltip("Força de estabilização para manter o carro de pé")]
    public float forcaEstabilidadeAerea = 400f;

    [Tooltip("Multiplicador da resposta aérea em relação ao controle terrestre")]
    [Range(0.5f, 2f)]
    public float sensibilidadeAerea = 1.2f;

    [Tooltip("Limite máximo de rotação angular no ar (graus/segundo)")]
    public float limiteRotacaoAerea = 180f;

    [Tooltip("Tempo mínimo no ar antes de ativar controle aéreo")]
    public float delayAtivacaoControleAereo = 0.2f;

    [Header("Sistema de Aterrissagem")]
    [Tooltip("Tempo para restaurar tração completa após aterrissar")]
    public float tempoRestauracaoTracao = 0.5f;

    [Tooltip("Redução do controle aéreo ao aproximar do chão")]
    public float reducaoControlePertoDoChao = 0.3f;

    [Tooltip("Altura mínima para considerar que está perto do chão")]
    public float alturaMinimaPertoDoChao = 3f;
    #endregion
    

    #region State Variables
    // Input State (recebido do SCR_CarInput)
    private float _steeringInput;
    private float _throttleInput;
    private float _brakeInput;
    private bool _handbrakeInput;
    private bool _controleAereoAtivo = false;
    private float _tempoNoAr = 0f;
    private bool _preparandoParaAterrissar = false;
    private bool _aterrissouRecentemente = false;
    private float _tempoDesdeAterrissagem = 0f;
    private Vector3 _velocidadeAereaInicial;
    private float _tempoDesdePerdaDeChao = 0f;
    private bool _noChaoComDelay = true;
    private const float DELAY_PERDA_DE_CHAO = 0.2f;

    
    // Physics State
    private float _currentSpeed;
    private int _wheelsOnGround;
    private bool _isGrounded;
    private bool _isDrifting;
    private bool _isBraking;
    private float _currentTurboEnergy;
    private bool _isTurboActive;
    
    // Stability Systems
    private float[] _suspensionCompression;
    private WheelHit[] _wheelHits;
    Vector3 GetWheelPosition(WheelCollider wheel)
    {
        Vector3 pos;
        Quaternion rot;
        wheel.GetWorldPose(out pos, out rot);
        return pos;
    }
    
    // Loop Mode
    private bool _originalGravity = true;
    #endregion

    #region Public Properties
    public float CurrentSpeed => _currentSpeed;
    public float SpeedKMH => _currentSpeed * 3.6f;
    public bool IsGrounded => _isGrounded;
    public bool IsDrifting => _isDrifting;
    public bool IsBraking => _isBraking;
    public Rigidbody VehicleRB => _rb;
    public bool IsTurboActive => _isTurboActive;
    public float TurboEnergy => _currentTurboEnergy;
    public float TurboEnergyNormalized => _currentTurboEnergy / maxTurboEnergy;
    public bool LoopModeActive => _loopModeActive;
    public bool ControleAereoAtivo => _controleAereoAtivo;
    #endregion

    #region Steering Smoothing System
    [Header("Steering Smoothing System")]
    [Tooltip("Tempo para atingir o steer máximo (segundos)")]
    public float steeringRampTime = 0.3f;

    [Tooltip("Curva de resposta do volante")]
    public AnimationCurve steeringResponseCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Tooltip("Redução de steer baseada na velocidade")]
    public AnimationCurve speedSteeringReduction = new AnimationCurve(
        new Keyframe(0, 1),    // Parado: 100% steer
        new Keyframe(20, 0.8f), // 20 km/h: 80%
        new Keyframe(50, 0.6f), // 50 km/h: 60%
        new Keyframe(80, 0.4f), // 80 km/h: 40%
        new Keyframe(100, 0.3f) // 100 km/h: 30%
    );

    [Tooltip("Máxima taxa de mudança do ângulo (graus/segundo)")]
    public float maxSteeringRate = 720f; // 720°/s = 2 voltas completas por segundo

    [Tooltip("Redução de steer durante derrapagem")]
    [Range(0f, 1f)]
    public float driftSteeringReduction = 0.7f;

    [Tooltip("Aumento de sensibilidade no ar")]
    [Range(0.5f, 2f)]
    public float aerialSteeringMultiplier = 1.5f;

    // Variáveis de estado
    private float _currentSteeringInput = 0f;
    private float _targetSteeringInput = 0f;
    private float _steeringVelocity = 0f;
    private float _lastAppliedSteering = 0f;
    private float _actualSteeringAngle = 0f;
    #endregion

    #region Unity Lifecycle
    private void Awake()
    {
        if (_rb == null) _rb = GetComponent<Rigidbody>();
        
        InitializeWheels();
        StoreOriginalRotations();
        CategorizeWheels();
        
        _suspensionCompression = new float[_wheels.Length];
        _wheelHits = new WheelHit[_wheels.Length];
    }
    
    private void Start()
    {
        if (_centerOfMass != null)
            _rb.centerOfMass = _centerOfMass.localPosition;

        if (steeringResponseCurve == null || steeringResponseCurve.keys.Length == 0)
        {
            // Curva padrão: ligeiramente exponencial para mais controle em baixos ângulos
            steeringResponseCurve = new AnimationCurve(
                new Keyframe(0, 0, 0, 1.5f),     // Início: mais sensível
                new Keyframe(0.5f, 0.7f, 1, 1), // Meio: linear
                new Keyframe(1, 1, 0.5f, 0)     // Fim: menos sensível
            );
        }
        
        ConfigureSuspension();
        ApplyGripSettings();
        _currentTurboEnergy = maxTurboEnergy;
        
        _originalGravity = _rb.useGravity;
    }
    
    private void Update()
    {
        UpdateWheelVisuals();
        UpdateTurboSystem();
        UpdateTurboParticles();
    }
    
    private void FixedUpdate()
    {
        // 1. PRIMEIRO: Verificar estado atual
        CalculateCurrentSpeed();
        CheckGroundStatus();
        
        // 2. SEGUNDO: Verificar se está no ar ANTES de tudo!
        bool usandoControleAereo = VerificarSeEstaNoAr();
        
        // 3. TERCEIRO: Cálculos de suspensão
        CalculateSuspensionCompression();
        
        // 4. QUARTO: Decidir qual física aplicar
        if (!usandoControleAereo)
        {
            //  Apenas NO CHÃO: estabilidade + física terrestre
            ApplyStabilitySystems(); // Anti-roll SÓ no chão
            ApplyGroundPhysics();
        }
        else
        {
            //  NO AR: apenas controle aéreo
            AplicarControleAereo();
            
            // NÃO aplicar anti-roll no ar!
            // ApplyStabilitySystems(); // REMOVER DAQUI
        }
        
        // 5. QUINTO: Sistemas auxiliares (funcionam sempre)
        ApplyNaturalResistance();
        ApplyDownforce();
    }
    #endregion

    #region Input Interface (Chamado pelo SCR_CarInput)
    public void SetSteering(float input) 
    {
        _steeringInput = Mathf.Clamp(input, -1f, 1f);
        _targetSteeringInput = _steeringInput;
    }
    public void SetThrottle(float input) => _throttleInput = Mathf.Clamp(input, -1f, 1f);
    public void SetBrake(float input) => _brakeInput = Mathf.Clamp01(input);
    public void SetHandbrake(bool active) => _handbrakeInput = active;

    public void ActivateTurbo()
    {
        if (!_isTurboActive && _currentTurboEnergy > 10f)
        {
            _isTurboActive = true;
            ApplyTurboBoost();
        }
    }

    public void DeactivateTurbo()
    {
        if (_isTurboActive)
        {
            _isTurboActive = false;
        }
    }
    
    public void ToggleLoopMode(bool active)
    {
        if (active == _loopModeActive) return;
        
        _loopModeActive = active;
        ApplyLoopModePhysics(active);
    }
    #endregion

    #region Wheel System Methods
    private void InitializeWheels()
    {
        for (int i = 0; i < _wheels.Length; i++)
        {
            if (_wheels[i].collider == null)
            {
                Debug.LogError($"Wheel collider not assigned at index {i}!");
                continue;
            }
            
            // Configuração otimizada do WheelCollider
            _wheels[i].collider.ConfigureVehicleSubsteps(5, 12, 15);
            _wheels[i].collider.mass = 20; // Massa realista para estabilidade
            _wheels[i].collider.wheelDampingRate = 0.25f;
        }
    }
    
    private void StoreOriginalRotations()
    {
        foreach (WheelData wheel in _wheels)
        {
            if (wheel.visual != null)
            {
                wheel.originalVisualRotation = wheel.visual.localEulerAngles;
            }
        }
    }
    
    private void CategorizeWheels()
    {
        _frontWheels.Clear();
        _rearWheels.Clear();
        _steerWheels.Clear();
        _driveWheels.Clear();
        
        foreach (WheelData wheel in _wheels)
        {
            if (wheel.isFrontWheel)
                _frontWheels.Add(wheel);
            else
                _rearWheels.Add(wheel);
                
            if (wheel.canSteer)
                _steerWheels.Add(wheel);
                
            if (wheel.canDrive)
                _driveWheels.Add(wheel);
        }
    }
    
    private void ConfigureSuspension()
    {
        foreach (WheelData wheel in _wheels)
        {
            if (wheel.collider == null) continue;
            
            JointSpring spring = wheel.collider.suspensionSpring;
            spring.spring = suspensionSpringRate;
            spring.damper = suspensionDamper;
            wheel.collider.suspensionSpring = spring;
            wheel.collider.suspensionDistance = suspensionDistance;
            wheel.collider.forceAppPointDistance = 0f;
        }
    }
    
    private void ApplyGripSettings()
    {
        foreach (WheelData wheel in _wheels)
        {
            if (wheel.collider == null) continue;
            
            // Sistema de grip DIFERENCIADO como no original
            float lateralGrip = gripLateralHigh;
            float forwardGrip = gripForwardHigh;
            
            // Ajuste por posição da roda
            if (wheel.isFrontWheel)
            {
                lateralGrip *= 1.05f;  // +5% para precisão
                forwardGrip *= 1.05f;  // +5% para tração
            }
            else
            {
                lateralGrip *= 0.95f;  // -5% para estabilidade
                forwardGrip *= 0.95f;  // -5% para controle
            }
            
            SetWheelFrictionCurveLateral(wheel.collider, extremumSlipLateralHigh, lateralGrip, 
                                        asymptoteSlipHigh, asymptoteValueHigh, gripStiffness);
            SetWheelFrictionCurveFrontal(wheel.collider, extremumSlipForwardHigh, forwardGrip, 
                                        asymptoteSlipHigh, asymptoteValueHigh, gripStiffness);
        }
    }
    
    private void SetWheelFrictionCurveLateral(WheelCollider wheel, float extremumSlip, float extremumValue, 
                                             float asymptoteSlip, float asymptoteValue, float stiffness)
    {
        WheelFrictionCurve sideways = wheel.sidewaysFriction;
        sideways.extremumSlip = extremumSlip;
        sideways.extremumValue = extremumValue;
        sideways.asymptoteSlip = asymptoteSlip;
        sideways.asymptoteValue = asymptoteValue;
        sideways.stiffness = stiffness;
        wheel.sidewaysFriction = sideways;
    }
    
    private void SetWheelFrictionCurveFrontal(WheelCollider wheel, float extremumSlip, float extremumValue, 
                                             float asymptoteSlip, float asymptoteValue, float stiffness)
    {
        WheelFrictionCurve forward = wheel.forwardFriction;
        forward.extremumSlip = extremumSlip;
        forward.extremumValue = extremumValue;
        forward.asymptoteSlip = asymptoteSlip;
        forward.asymptoteValue = asymptoteValue;
        forward.stiffness = stiffness;
        wheel.forwardFriction = forward;
    }
    
    private void UpdateWheelVisuals()
    {
        float deltaTime = Time.deltaTime;
        
        foreach (WheelData wheel in _wheels)
        {
            if (wheel.visual == null || wheel.collider == null) continue;
            
            // 1. OBTER POSIÇÃO DA FÍSICA (suspensão real)
            Vector3 position;
            Quaternion rotation;
            wheel.collider.GetWorldPose(out position, out rotation);
            
            // 2. APLICAR POSIÇÃO (suspensão) da física - APENAS Y
            //wheel.visual.position = new Vector3(wheel.visual.position.x, position.y, wheel.visual.position.z);
            //MEXER NA POSIÇÂO FAZ DESSINCRONIZAR E A RODA PARAR NA PQP

            // 3. CALCULAR ROTAÇÃO DA RODA (GIRO)
            float wheelRotationSpeed = CalculateWheelRotationSpeed(wheel);
            float accumulatedRotation = wheelRotationSpeed * deltaTime;
            
            // 4. CALCULAR ÂNGULO DE ESTERÇO
            float steerAngle = CalculateSteerAngle(wheel, deltaTime);
            
            // 5. APLICAR ROTAÇÃO VISUAL (estilo arcade - rodas não respondem fisicamente)
            ApplyWheelVisualRotation(wheel, steerAngle, accumulatedRotation);
            
            // 6. APLICAR À FÍSICA (para o WheelCollider)
            wheel.collider.steerAngle = steerAngle;
        }
    }

    private float CalculateWheelRotationSpeed(WheelData wheel)
    {
        float speed = 0f;
        
        if (Mathf.Abs(_throttleInput) > 0.1f)
        {
            // Acelerando ou dando ré
            speed = _currentSpeed * 360f / (2f * Mathf.PI * 0.3f); // Fórmula baseada em raio da roda (0.3m)
            speed *= Mathf.Sign(_throttleInput);
            
            // Se estiver em ré e velocidade for positiva, inverter
            if (_throttleInput < 0 && _currentSpeed > 0)
            {
                speed = -Mathf.Abs(speed);
            }
        }
        else if (_isBraking && _brakeInput > 0.1f)
        {
            // Freando - rodas giram mais devagar
            speed = _currentSpeed * 180f / (2f * Mathf.PI * 0.3f) * 0.5f;
        }
        else if (_currentSpeed > 0.1f)
        {
            // Inércia
            speed = _currentSpeed * 180f / (2f * Mathf.PI * 0.3f);
        }
        
        return speed;
    }

    private float CalculateSteerAngle(WheelData wheel, float deltaTime)
    {
        if (!wheel.canSteer) return 0f;
        
        float targetSteerAngle = _steeringInput * maxSteeringAngle;
        float maxSteerRate = 720f * deltaTime;
        
        // Suavizar transição
        float currentSteerAngle = wheel.collider.steerAngle;
        float newSteerAngle = Mathf.MoveTowards(currentSteerAngle, targetSteerAngle, maxSteerRate);
        
        if (rearSteeringRatio > 0.01f && !wheel.isFrontWheel)
        {
            //Usar steeringInput diretamente SEM suavização para 4WS
            float speedFactor = Mathf.Clamp01(_currentSpeed / 20f);
            
            // Direção PROPORCIONAL (não inversa)
            // - A baixa velocidade: mesma direção das dianteiras (melhor manobrabilidade)
            // - A alta velocidade: direção oposta (mais estável)
            float rearDirection = Mathf.Lerp(1.0f, -0.5f, speedFactor); // De 1.0 para -0.5
            
            // Cálculo DIRETO sem suavização para evitar tremulação
            newSteerAngle = _steeringInput * maxSteeringAngle * rearSteeringRatio * rearDirection;
            
            // Apenas um pouco de suavização para rodas traseiras
            newSteerAngle = Mathf.MoveTowards(currentSteerAngle, newSteerAngle, maxSteerRate);
        }
        
        return newSteerAngle;
    }

    private void ApplyWheelVisualRotation(WheelData wheel, float steerAngle, float wheelRotation)
    {
        // Determinar direção do giro (esquerda vs direita)
        bool isLeftWheel = IsLeftWheel(wheel);
        float rotationDirection = isLeftWheel ? 1f : -1f;
        
        // Acumular rotação para evitar perda de precisão
        if (!_wheelRotationAccumulated.ContainsKey(wheel))
        {
            _wheelRotationAccumulated[wheel] = 0f;
        }
        _wheelRotationAccumulated[wheel] += wheelRotation * rotationDirection;
        
        // Manter o ângulo acumulado dentro de 0-360 graus
        _wheelRotationAccumulated[wheel] %= 360f;
        
        // Criar rotação final:
        // 1. Rotação base original
        // 2. Adicionar esterço (Y)
        // 3. Adicionar rotação do giro (X para rodas de corrida, Z para carros normais)
        
        // PARA CARROS DE CORRIDA (roda vertical): giro no eixo X
        // PARA CARROS NORMAIS (roda inclinada): giro no eixo Z
        
        Vector3 finalEuler = wheel.originalVisualRotation;
        
        // Aplicar esterço (Y)
        finalEuler.y += steerAngle;
        
        // Aplicar giro da roda
        // Assumindo que a roda visual está modelada com eixo de rotação no X (como em muitos assets)
        finalEuler.x += _wheelRotationAccumulated[wheel];
        
        // Alternativa: se as rodas estiverem modeladas com eixo no Z:
        // finalEuler.z += _wheelRotationAccumulated[wheel];
        
        wheel.visual.localEulerAngles = finalEuler;
        
        // Debug para verificar rotação
        if (Debug.isDebugBuild && wheel.isFrontWheel)
        {
            Debug.DrawRay(wheel.visual.position, wheel.visual.right * 0.5f, Color.red);
            Debug.DrawRay(wheel.visual.position, wheel.visual.up * 0.5f, Color.green);
            Debug.DrawRay(wheel.visual.position, wheel.visual.forward * 0.5f, Color.blue);
        }
    }
    #endregion

    #region Core Physics Methods
    private void CalculateCurrentSpeed()
    {
        Vector3 localVelocity = transform.InverseTransformDirection(_rb.linearVelocity);
        _currentSpeed = Mathf.Abs(localVelocity.z);
    }
    
    private void CheckGroundStatus()
    {
        _wheelsOnGround = 0;
        foreach (WheelData wheel in _wheels)
        {
            if (wheel.isGrounded) _wheelsOnGround++;
        }
        
        //  SISTEMA DE DELAY PARA PERDA DE CHÃO
        bool estaNoChaoInstantaneo = _wheelsOnGround >= 2;
        
        if (estaNoChaoInstantaneo)
        {
            // Se está no chão agora: resetar timer e garantir que _noChaoComDelay = true
            _tempoDesdePerdaDeChao = 0f;
            _noChaoComDelay = true;
            _isGrounded = true;
        }
        else
        {
            // Se não está no chão: incrementar timer
            _tempoDesdePerdaDeChao += Time.fixedDeltaTime;
            
            //  Só considerar "não no chão" após 2 segundos
            if (_tempoDesdePerdaDeChao >= DELAY_PERDA_DE_CHAO)
            {
                _noChaoComDelay = false;
                _isGrounded = false;
            }
            else
            {
                //  Durante os 2 segundos de delay: ainda considerar como "no chão"
                _noChaoComDelay = true;
                _isGrounded = true;
            }
        }
        
        // Debug visual
        if (Debug.isDebugBuild && !estaNoChaoInstantaneo && _tempoDesdePerdaDeChao > 0)
        {
            Debug.Log($"Delay ativo: {_tempoDesdePerdaDeChao:F1}s / {DELAY_PERDA_DE_CHAO}s - Rodas no chão: {_wheelsOnGround}");
            Debug.DrawRay(transform.position, Vector3.up * 3f, 
                        Color.Lerp(Color.green, Color.red, _tempoDesdePerdaDeChao / DELAY_PERDA_DE_CHAO));
        }
    }

    private int ContarRodasNoChao()
    {
        int count = 0;
        foreach (var wheel in _wheels)
        {
            if (wheel.isGrounded) count++;
        }
        return count;
    }

    private bool VerificarSeEstaNoAr()
    {
        //  Usar _noChaoComDelay (com delay) em vez de _isGrounded (instantâneo)
        bool estaNoAr = !_noChaoComDelay;
        
        if (estaNoAr)
        {
            _tempoNoAr += Time.fixedDeltaTime;
            
            // Verificar se está perto do chão
            _preparandoParaAterrissar = VerificarProximidadeDoChao();
            
            // Ativar controle aéreo após delay
            if (!_controleAereoAtivo && _tempoNoAr >= delayAtivacaoControleAereo)
            {
                _controleAereoAtivo = true;
                _velocidadeAereaInicial = _rb.linearVelocity;
                
                Debug.Log($"🛫 Controle aéreo ATIVADO após {_tempoNoAr:F1}s no ar (delay de {delayAtivacaoControleAereo}s)");
            }
        }
        else
        {
            // Se tocou no chão
            if (_controleAereoAtivo)
            {
                _controleAereoAtivo = false;
                _tempoNoAr = 0f;
                _preparandoParaAterrissar = false;
                
                // Iniciar processo de restauração de tração
                IniciarRestauracaoTracao();
                
                Debug.Log($"🛬 Controle aéreo DESATIVADO - Aterrissou!");
            }
            else if (_aterrissouRecentemente)
            {
                // Continuar restauração de tração
                AtualizarRestauracaoTracao();
            }
            else
            {
                _tempoNoAr = 0f;
            }
        }

        return _controleAereoAtivo;
    }
    
    private void ApplyGroundPhysics()
    {
        if (_aterrissouRecentemente)
        {
            // Aplicar esterço reduzido durante restauração
            AplicarEstercoSuave();
            ApplyEngine();
        }
        else
        {
            ApplySteering();
            ApplyEngine();
        }
        
        ApplyBrakes();
        UpdateDriftState();
    }

    private float CalculateGradualSteering(float deltaTime)
    {
        // 1. Interpolação suave para o input alvo
        float smoothTime = steeringRampTime;
        
        // Reduzir tempo de resposta se estiver no ar (mais responsivo)
        if (_controleAereoAtivo)
        {
            smoothTime *= 0.5f;
        }
        
        _currentSteeringInput = Mathf.SmoothDamp(
            _currentSteeringInput,
            _targetSteeringInput,
            ref _steeringVelocity,
            smoothTime,
            maxSteeringRate,
            deltaTime
        );
        
        // 2. Aplicar curva de resposta
        float curvedInput = steeringResponseCurve.Evaluate(Mathf.Abs(_currentSteeringInput)) 
                        * Mathf.Sign(_currentSteeringInput);
        
        // 3. Redução baseada na velocidade
        float speedKMH = SpeedKMH;
        float speedFactor = speedSteeringReduction.Evaluate(speedKMH);
        
        // 4. Redução durante derrapagem
        if (_isDrifting)
        {
            speedFactor *= driftSteeringReduction;
            
            // Inversão de direção mais rápida durante drift
            if (Mathf.Sign(_targetSteeringInput) != Mathf.Sign(_currentSteeringInput))
            {
                speedFactor *= 1.2f; // 20% mais rápido para contra-steer
            }
        }
        
        // 5. Aumento de sensibilidade no ar
        if (_controleAereoAtivo)
        {
            speedFactor *= aerialSteeringMultiplier;
        }
        
        // 6. Redução progressiva em altas velocidades (existente)
        float speedReduction = Mathf.Lerp(1f, steeringReductionAtMaxSpeed, speedKMH / topSpeed);
        speedFactor *= speedReduction;
        
        // 7. Limitar taxa de mudança do ângulo real
        float targetAngle = curvedInput * maxSteeringAngle * speedFactor;
        float maxDeltaAngle = maxSteeringRate * deltaTime;
        
        _actualSteeringAngle = Mathf.MoveTowards(
            _lastAppliedSteering,
            targetAngle,
            maxDeltaAngle
        );
        
        _lastAppliedSteering = _actualSteeringAngle;
        
        return _actualSteeringAngle;
    }
    
    private void ApplySteering()
    {
        float steeringAngle = 0f;
        
        if (_aterrissouRecentemente)
        {
            // Usar sistema suave pós-aterrissagem
            steeringAngle = AplicarEstercoSuave();
        }
        else
        {
            // Usar sistema gradual completo
            steeringAngle = CalculateGradualSteering(Time.fixedDeltaTime);
        }
        
        foreach (WheelData wheel in _steerWheels)
        {
            if (wheel.canSteer)
            {
                float finalAngle = steeringAngle;
                
                // Aplicar 4WS se configurado
                if (rearSteeringRatio > 0.01f && !wheel.isFrontWheel)
                {
                    float speedFactor = Mathf.Clamp01(_currentSpeed / 20f);
                    float rearDirection = Mathf.Lerp(-0.5f, 0.2f, speedFactor);
                    finalAngle = steeringAngle * rearSteeringRatio * rearDirection;
                }
                
                wheel.collider.steerAngle = finalAngle;
            }
        }
        
        // Debug visual
        if (Debug.isDebugBuild)
        {
            Debug.DrawRay(transform.position, 
                transform.right * _currentSteeringInput * 2f, 
                Color.blue);
            Debug.DrawRay(transform.position + Vector3.up * 0.5f, 
                transform.right * _targetSteeringInput * 2f, 
                Color.red);
        }
    }
    
    private void ApplyEngine()
    {
        float currentTopSpeed = _isTurboActive ? turboTopSpeed : topSpeed;
        float currentAcceleration = _isTurboActive ? turboAcceleration : acceleration;
        
        bool canAccelerateForward = _throttleInput > 0 && _currentSpeed < currentTopSpeed;
        bool canReverse = _throttleInput < 0 && _currentSpeed < reverseSpeed;
        
        if (canAccelerateForward || canReverse)
        {
            float torque = currentAcceleration * _throttleInput;
            ApplyDrivetrainTorque(torque);
        }
        else
        {
            // Zerar torque quando não está acelerando
            foreach (WheelData wheel in _driveWheels)
            {
                if (wheel.canDrive)
                    wheel.collider.motorTorque = 0f;
            }
        }
    }
    
    private void ApplyDrivetrainTorque(float totalTorque)
    {
        float diffFactor = _steeringInput * differentialForce;
        
        // Contar rodas motorizadas por eixo
        int frontDriveCount = 0;
        int rearDriveCount = 0;
        
        foreach (WheelData wheel in _driveWheels)
        {
            if (wheel.isFrontWheel) frontDriveCount++;
            else rearDriveCount++;
        }
        
        foreach (WheelData wheel in _driveWheels)
        {
            if (!wheel.canDrive) continue;
            
            float wheelTorque = 0f;
            
            // APLICAR driveDistribution CORRETAMENTE
            if (wheel.isFrontWheel && frontDriveCount > 0)
            {
                // Rodas dianteiras: (1 - driveDistribution) do torque
                wheelTorque = (totalTorque * (1f - driveDistribution)) / frontDriveCount;
            }
            else if (!wheel.isFrontWheel && rearDriveCount > 0)
            {
                // Rodas traseiras: driveDistribution do torque
                wheelTorque = (totalTorque * driveDistribution) / rearDriveCount;
            }
            
            // Aplicar diferencial
            bool isLeftWheel = IsLeftWheel(wheel);
            float torqueMultiplier = isLeftWheel ? (1f - diffFactor) : (1f + diffFactor);
            
            wheel.collider.motorTorque = wheelTorque * torqueMultiplier;
        }
    }

    public void ConfigureDifferential(float newForce, float newLockRatio = 0.5f)
    {
        differentialForce = Mathf.Clamp01(newForce);
        
        // Aplicar configurações às rodas
        foreach (WheelData wheel in _wheels)
        {
            if (wheel.collider != null)
            {
                // Ajustar rigidez do diferencial
                WheelFrictionCurve forward = wheel.collider.forwardFriction;
                forward.stiffness = Mathf.Lerp(1.0f, 1.5f, newLockRatio);
                wheel.collider.forwardFriction = forward;
            }
        }
        
        Debug.Log($"Differential configured: Force={differentialForce:F2}, Lock={newLockRatio:F2}");
    }
    
    private bool IsLeftWheel(WheelData wheel)
    {
        // Usar o enum WheelPosition que já está definido na classe
        return wheel.position == WheelData.WheelPosition.FrontLeft || 
            wheel.position == WheelData.WheelPosition.RearLeft;
    }

    private bool IsRightWheel(WheelData wheel)
    {
        return wheel.position == WheelData.WheelPosition.FrontRight || 
            wheel.position == WheelData.WheelPosition.RearRight;
    }

    private bool IsFrontWheel(WheelData wheel)
    {
        return wheel.position == WheelData.WheelPosition.FrontLeft || 
            wheel.position == WheelData.WheelPosition.FrontRight;
    }

    private bool IsRearWheel(WheelData wheel)
    {
        return wheel.position == WheelData.WheelPosition.RearLeft || 
            wheel.position == WheelData.WheelPosition.RearRight;
    }
    
    private void ApplyBrakes()
    {
        _isBraking = _brakeInput > 0.1f || _handbrakeInput;
        
        foreach (WheelData wheel in _wheels)
        {
            if (_handbrakeInput && !wheel.isFrontWheel)
            {
                // Handbrake apenas nas rodas traseiras
                wheel.collider.brakeTorque = brakingForce * 0.8f;
            }
            else
            {
                wheel.collider.brakeTorque = brakingForce * _brakeInput;
            }
        }
    }
    #endregion

    #region Downforce System
    private void ApplyDownforce()
    {
        if (downforce <= 0) return;
        
        // Aplicar downforce apenas se estiver no chão ou no modo loop
        bool shouldApplyDownforce = _noChaoComDelay || _loopModeActive;
        if (!shouldApplyDownforce) return;
        
        // Calcular velocidade
        float speed = _rb.linearVelocity.magnitude;
        
        // Garantir velocidade mínima no modo loop
        if (speed < 5f && _loopModeActive)
        {
            speed = 5f;
        }
        
        // Calcular downforce (proporcional ao quadrado da velocidade)
        float calculatedDownforce = speed * speed * speed * downforce;
        
        // Adicionar downforce extra no modo loop
        if (_loopModeActive)
        {
            calculatedDownforce += additionalLoopDownforce * speed;
        }
        
        // Limitar força máxima
        float finalForce = Mathf.Min(calculatedDownforce, maxDownforce);
        
        // Aplicar força para baixo
        _rb.AddForce(-transform.up * finalForce);
    }
    
    private void ApplyLoopModePhysics(bool active)
    {
        if (active)
        {
            _rb.useGravity = false;
        }
        else
        {
            _rb.useGravity = _originalGravity;
        }
    }
    #endregion

    #region Stability Systems - Sistema Antigo que Funciona
    
    private void ApplyStabilitySystems()
    {
        if (!_noChaoComDelay) return;
        
        // Aplicar anti-roll se tiver pelo menos 2 rodas de cada lado
        if (_frontWheels.Count >= 2 && _rearWheels.Count >= 2)
        {
            ApplyAntiRollForce();
        }
        
        StabilizeInCurves();

        DampenExcessiveRotation();
    }
    
    private void ApplyAntiRollForce()
    {
        // Anti-roll frontal
        ApplyAntiRollAxle(_frontWheels[0], _frontWheels[1], 0.6f);
        
        // Anti-roll traseiro
        ApplyAntiRollAxle(_rearWheels[0], _rearWheels[1], 0.4f);
        
        // Anti-roll lateral (entre eixos)
        ApplyAntiRollLateral();
    }
    
    private void ApplyAntiRollAxle(WheelData leftWheel, WheelData rightWheel, float axisFactor)
    {
        float leftComp = leftWheel.currentCompression;
        float rightComp = rightWheel.currentCompression;
        float diff = leftComp - rightComp;
        
        if (Mathf.Abs(diff) > 0.05f)
        {
            float force = diff * antiRollForce * axisFactor;
            
            Vector3 leftWheelPos = GetWheelWorldPosition(leftWheel.collider);
            Vector3 rightWheelPos = GetWheelWorldPosition(rightWheel.collider);
            
            if (leftWheel.isGrounded)
                _rb.AddForceAtPosition(transform.up * force, leftWheelPos);
            
            if (rightWheel.isGrounded)
                _rb.AddForceAtPosition(-transform.up * force, rightWheelPos);
        }
    }

    //  MÉTODO PARA OBTER POSIÇÃO REAL DA RODA
    private Vector3 GetWheelWorldPosition(WheelCollider wheel)
    {
        Vector3 position;
        Quaternion rotation;
        wheel.GetWorldPose(out position, out rotation);
        return position;
    }

    //  RESTAURAR CÁLCULO DE COMPRESSÃO DO SISTEMA ANTIGO
    private void CalculateSuspensionCompression()
    {
        for (int i = 0; i < _wheels.Length; i++)
        {
            if (_wheels[i].collider.GetGroundHit(out _wheelHits[i]))
            {
                // ✅ Usar spring rate INDIVIDUAL da roda
                float springRate = _wheels[i].collider.suspensionSpring.spring;
                float maxForca = springRate * 1.5f; // Usar valor da roda, não global
                
                float forca = _wheelHits[i].force;
                _suspensionCompression[i] = Mathf.Clamp01(forca / maxForca);
                _wheels[i].currentCompression = _suspensionCompression[i];
            }
            else
            {
                _suspensionCompression[i] = 0f;
                _wheels[i].currentCompression = 0f;
            }
        }
    }

    //  ADICIONAR: Sistema anti-roll lateral como no antigo
    private void ApplyAntiRollLateral()
    {
        if (_frontWheels.Count < 2 || _rearWheels.Count < 2) return;
        
        float compressaoMediaEsquerda = (_frontWheels[0].currentCompression + _rearWheels[0].currentCompression) * 0.5f;
        float compressaoMediaDireita = (_frontWheels[1].currentCompression + _rearWheels[1].currentCompression) * 0.5f;
        
        float diferencaLateral = compressaoMediaEsquerda - compressaoMediaDireita;
        
        if (Mathf.Abs(diferencaLateral) > 0.03f)
        {
            float forcaLateral = diferencaLateral * antiRollForce * 0.3f;
            
            // Aplicar torque para estabilizar
            Vector3 torqueAntiRoll = transform.forward * forcaLateral;
            _rb.AddTorque(torqueAntiRoll);
        }
    }
    
    private void StabilizeInCurves()
    {
        if (!_controleAereoAtivo && Mathf.Abs(_steeringInput) > 0.1f && _currentSpeed > 10f)
        {
            Vector3 localAngularVelocity = transform.InverseTransformDirection(_rb.angularVelocity);
            
            // Reduzir rotação excessiva durante curvas
            if (Mathf.Abs(localAngularVelocity.y) > 0.5f)
            {
                float reduction = -localAngularVelocity.y * curveStabilization * 0.5f;
                _rb.AddRelativeTorque(0, reduction, 0, ForceMode.Acceleration);
                
                // Amortecer rotação vertical também
                if (Mathf.Abs(localAngularVelocity.x) > 0.3f)
                {
                    _rb.AddRelativeTorque(-localAngularVelocity.x * curveStabilization * 0.3f, 0, 0, ForceMode.Acceleration);
                }
            }
        }
    }

    private void DampenExcessiveRotation()
    {
        if (_currentSpeed > 1f)
        {
            Vector3 localAngVel = transform.InverseTransformDirection(_rb.angularVelocity);
            
            //  AUMENTAR força de amortecimento durante derrapagem
            float dampingMultiplier = _isDrifting ? 1.5f : 1.0f;
            
            // Amortecer rotação vertical (capotamento) - CRÍTICO!
            if (Mathf.Abs(localAngVel.x) > 0.8f)
            {
                Vector3 damping = new Vector3(-localAngVel.x * 1000f * dampingMultiplier, 0, 0);
                _rb.AddRelativeTorque(damping * Time.fixedDeltaTime, ForceMode.Force);
                
                // Debug
                if (Debug.isDebugBuild && Mathf.Abs(localAngVel.x) > 2f)
                {
                    Debug.DrawRay(transform.position, Vector3.up * 3f, Color.red);
                }
            }
            
            // Amortecer rotação de guinada (giro) - MUITO IMPORTANTE!
            if (Mathf.Abs(localAngVel.y) > 1.5f)
            {
                Vector3 damping = new Vector3(0, -localAngVel.y * 800f * dampingMultiplier, 0);
                _rb.AddRelativeTorque(damping * Time.fixedDeltaTime, ForceMode.Force);
                
                // Debug
                if (Debug.isDebugBuild && Mathf.Abs(localAngVel.y) > 3f)
                {
                    Debug.DrawRay(transform.position, Vector3.up * 2f, Color.yellow);
                }
            }
            
            //  NOVO: Amortecer rotação lateral (rolamento)
            if (Mathf.Abs(localAngVel.z) > 0.5f)
            {
                Vector3 damping = new Vector3(0, 0, -localAngVel.z * 600f * dampingMultiplier);
                _rb.AddRelativeTorque(damping * Time.fixedDeltaTime, ForceMode.Force);
            }
        }
    }
    #endregion

    #region Natural Resistance
    private void ApplyNaturalResistance()
    {
        if (Mathf.Abs(_throttleInput) < 0.1f && _currentSpeed > 0.1f)
        {
            Vector3 resistance = -_rb.linearVelocity.normalized * naturalResistance;
            _rb.AddForce(resistance, ForceMode.Force);
        }
    }
    #endregion

    #region Turbo System
    private void UpdateTurboSystem()
    {
        if (_isTurboActive)
        {
            _currentTurboEnergy -= turboConsumptionRate * Time.deltaTime;
            
            if (_currentTurboEnergy <= 0f)
            {
                _currentTurboEnergy = 0f;
                DeactivateTurbo();
            }
        }
        else
        {
            if (_currentTurboEnergy < maxTurboEnergy)
            {
                _currentTurboEnergy += turboRechargeRate * Time.deltaTime;
                _currentTurboEnergy = Mathf.Min(_currentTurboEnergy, maxTurboEnergy);
            }
        }
    }
    
    private void ApplyTurboBoost()
    {
        if (_rb != null)
        {
            _rb.AddForce(transform.forward * 15000f, ForceMode.Impulse);
        }
    }
    
    private void UpdateTurboParticles()
    {
        if (turboParticles != null)
        {
            if (_isTurboActive && !turboParticles.isPlaying)
            {
                turboParticles.Play();
            }
            else if (!_isTurboActive && turboParticles.isPlaying)
            {
                turboParticles.Stop();
            }
        }
    }
    #endregion

    #region Drift Detection
    private void UpdateDriftState()
    {
        Vector3 localVelocity = transform.InverseTransformDirection(_rb.linearVelocity);
        float lateralSpeed = Mathf.Abs(localVelocity.x);
        float forwardSpeed = Mathf.Abs(localVelocity.z);
        
        _isDrifting = lateralSpeed > 2f && forwardSpeed > 5f && Mathf.Abs(_steeringInput) > 0.3f;
    }
    #endregion

    #region Aerial Control System

    private bool VerificarProximidadeDoChao()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position, Vector3.down, out hit, alturaMinimaPertoDoChao))
        {
            // Se estiver descendo
            Vector3 velocidade = _rb.linearVelocity;
            if (velocidade.y < -2f)
            {
                return true;
            }
        }
        return false;
    }

    private void AplicarControleAereo()
    {
        if (!_controleAereoAtivo) return;

        // Obter entrada do jogador
        float entradaHorizontal = Mathf.Clamp(_steeringInput, -1f, 1f);
        
        // Reduzir controle se estiver perto do chão
        float fatorControle = _preparandoParaAterrissar ? reducaoControlePertoDoChao : 1f;
        
        // Se não há entrada, apenas estabilizar
        if (Mathf.Abs(entradaHorizontal) < 0.25f)
        {
            AplicarEstabilidadeAerea();
            return;
        }

        // Aplicar fator de redução se estiver perto do chão
        float velocidadeNormalizada = Mathf.Clamp01(_currentSpeed / topSpeed);
        float fatorVelocidade = Mathf.Lerp(1f, 0.3f, velocidadeNormalizada);
        
        float torqueHorizontal = entradaHorizontal * forcaRotacaoAerea * sensibilidadeAerea * 
                            fatorVelocidade * fatorControle;
        
        Vector3 torqueGlobal = transform.TransformDirection(new Vector3(0, torqueHorizontal * Time.fixedDeltaTime, 0));
        
        // Limitar torque máximo
        float torqueMagnitude = torqueGlobal.magnitude;
        float limiteTorqueRad = limiteRotacaoAerea * Mathf.Deg2Rad * Time.fixedDeltaTime;
        
        if (torqueMagnitude > limiteTorqueRad)
        {
            torqueGlobal = torqueGlobal.normalized * limiteTorqueRad;
        }
        
        // Aplicar torque
        _rb.AddTorque(torqueGlobal, ForceMode.VelocityChange);
        
        // Aplicar estabilização
        AplicarEstabilidadeAerea();
        
        // Controlar direção MAS com suavização extra se perto do chão
        if (!_preparandoParaAterrissar)
        {
            ControlarDirecaoVelocidadeNoAr();
        }
    }

    private void AplicarEstabilidadeAerea()
    {
        if (!_controleAereoAtivo) return;
    
        Vector3 up = transform.up;
        Vector3 worldUp = Vector3.up;
        float anguloInclinacao = Vector3.Angle(up, worldUp);
        
        //  AUMENTAR força de correção
        if (anguloInclinacao > 10f)
        {
            Vector3 eixoCorrecao = Vector3.Cross(up, worldUp).normalized;
            float forcaCorrecao = anguloInclinacao * forcaEstabilidadeAerea * 2f * Time.fixedDeltaTime;
            
            if (eixoCorrecao.magnitude > 0.01f)
            {
                _rb.AddTorque(eixoCorrecao * forcaCorrecao, ForceMode.Force);
            }
        }
        
        //  NOVO: Manter velocidade linear estável
        Vector3 velocidadeHorizontal = new Vector3(_rb.linearVelocity.x, 0, _rb.linearVelocity.z);
        if (velocidadeHorizontal.magnitude > 5f)
        {
            // Aplicar downforce artificial no ar para estabilidade
            //_rb.AddForce(-transform.up * 500f * Time.fixedDeltaTime);
        }
        
        // Amortecer rotações extras se perto do chão
        if (_preparandoParaAterrissar)
        {
            Vector3 velocidadeAngularLocal = transform.InverseTransformDirection(_rb.angularVelocity);
            
            // Amortecer rotações laterais
            if (Mathf.Abs(velocidadeAngularLocal.x) > 0.5f)
            {
                Vector3 amortecimento = new Vector3(-velocidadeAngularLocal.x * 200f, 0, 0);
                _rb.AddRelativeTorque(amortecimento * Time.fixedDeltaTime, ForceMode.Force);
            }
            
            // Amortecer rotação vertical
            if (Mathf.Abs(velocidadeAngularLocal.z) > 0.5f)
            {
                Vector3 amortecimento = new Vector3(0, 0, -velocidadeAngularLocal.z * 200f);
                _rb.AddRelativeTorque(amortecimento * Time.fixedDeltaTime, ForceMode.Force);
            }
        }
    }

    private void ControlarDirecaoVelocidadeNoAr()
    {
        if (!_controleAereoAtivo) return;

        Vector3 velocidadeAtual = _rb.linearVelocity;
        
        // Componente horizontal atual
        Vector3 velocidadeHorizontal = new Vector3(velocidadeAtual.x, 0, velocidadeAtual.z);
        float magnitudeHorizontal = velocidadeHorizontal.magnitude;
        
        if (magnitudeHorizontal < 2f) return;
        
        // Direção que o carro está olhando (sem componente Y)
        Vector3 direcaoCarro = transform.forward;
        direcaoCarro.y = 0;
        
        if (direcaoCarro.magnitude < 0.01f) return;
        direcaoCarro.Normalize();
        
        // Calcular ângulo atual entre velocidade e direção do carro
        Vector3 direcaoVelocidade = velocidadeHorizontal.normalized;
        float anguloAtual = Vector3.Angle(direcaoVelocidade, direcaoCarro);
        
        // Só corrigir se houver desalinhamento significativo
        if (anguloAtual > 5f)
        {
            // Calcular taxa máxima de rotação baseada na velocidade
            float velocidadeNormalizada = Mathf.Clamp01(magnitudeHorizontal / 50f);
            float taxaRotacaoMaxima = Mathf.Lerp(180f, 45f, velocidadeNormalizada);
            
            // Usar RotateTowards para girar a direção
            Vector3 novaDirecaoVelocidade = Vector3.RotateTowards(
                direcaoVelocidade,          // Direção atual
                direcaoCarro,               // Direção alvo
                taxaRotacaoMaxima * Mathf.Deg2Rad * Time.fixedDeltaTime,
                0f
            );
            
            novaDirecaoVelocidade.Normalize();
            
            // Aplicar nova direção mantendo mesma magnitude
            Vector3 novaVelocidadeHorizontal = novaDirecaoVelocidade * magnitudeHorizontal;
            
            // Manter componente vertical (gravidade)
            _rb.linearVelocity = new Vector3(
                novaVelocidadeHorizontal.x,
                velocidadeAtual.y,
                novaVelocidadeHorizontal.z
            );
        }
    }

    private void IniciarRestauracaoTracao()
    {
        _aterrissouRecentemente = true;
        _tempoDesdeAterrissagem = 0f;
        
        // Reduzir imediatamente qualquer rotação residual
        _rb.angularVelocity *= 0.3f;
        
        Debug.Log("Iniciando restauração de tração...");
    }

    private void AtualizarRestauracaoTracao()
    {
        _tempoDesdeAterrissagem += Time.deltaTime;
        
        float progresso = Mathf.Clamp01(_tempoDesdeAterrissagem / tempoRestauracaoTracao);
        
        // Restaurar tração gradualmente
        RestaurarTracaoGradual(progresso);
        
        if (progresso >= 1f)
        {
            _aterrissouRecentemente = false;
            Debug.Log("Tração completamente restaurada!");
        }
    }

    private void RestaurarTracaoGradual(float progresso)
    {
        // Aumentar rigidez das rodas gradualmente
        foreach (WheelData wheel in _wheels)
        {
            if (wheel.isGrounded)
            {
                // Restaurar fricção lateral
                WheelFrictionCurve lateral = wheel.collider.sidewaysFriction;
                lateral.stiffness = Mathf.Lerp(0.3f, 1.2f, progresso);
                wheel.collider.sidewaysFriction = lateral;
                
                // Restaurar fricção frontal
                WheelFrictionCurve forward = wheel.collider.forwardFriction;
                forward.stiffness = Mathf.Lerp(0.3f, 1.2f, progresso);
                wheel.collider.forwardFriction = forward;
            }
        }
        
        // Reduzir rotação angular residual
        if (progresso < 0.5f)
        {
            _rb.angularVelocity *= (1f - progresso * 0.5f);
        }
    }

    private float AplicarEstercoSuave()
    {
        float progresso = Mathf.Clamp01(_tempoDesdeAterrissagem / tempoRestauracaoTracao);
        
        // Usar o sistema gradual mas com redução extra
        float gradualAngle = CalculateGradualSteering(Time.fixedDeltaTime);
        
        // Redução progressiva do esterço enquanto restaura tração
        float fatorReducao = Mathf.Lerp(0.3f, 1f, progresso);
        
        return gradualAngle * fatorReducao;
    }
    #endregion

    #region Debug Methods
    public void PrintPhysicsStatus()
    {
        string status = $"Physics Status:\n" +
                       $"Speed: {SpeedKMH:N1} km/h\n" +
                       $"Grounded: {_isGrounded} ({_wheelsOnGround}/{_wheels.Length} wheels)\n" +
                       $"Drifting: {_isDrifting}\n" +
                       $"Braking: {_isBraking}\n" +
                       $"Turbo: {_isTurboActive} ({_currentTurboEnergy:N0}/{maxTurboEnergy})\n" +
                       $"Loop Mode: {_loopModeActive}";
        Debug.Log(status);
    }
    
    public void PrintWheelStatus()
    {
        string status = "Wheel Status:\n";
        for (int i = 0; i < _wheels.Length; i++)
        {
            status += $"Wheel {i}: Grounded={_wheels[i].isGrounded}, " +
                     $"Motor={_wheels[i].collider.motorTorque:N0}, " +
                     $"Brake={_wheels[i].collider.brakeTorque:N0}, " +
                     $"Steer={_wheels[i].collider.steerAngle:N1}°\n";
        }
        Debug.Log(status);
    }
    #endregion
    
    #region Configuration Methods
    public void ConfigureForTesting()
    {
        // Configuração inicial para testes
        acceleration = 5000f;
        brakingForce = 1000000f;
        maxSteeringAngle = 25f;
        
        gripLateralHigh = 4f;
        gripForwardHigh = 3f;
        
        downforce = 200f;
        maxDownforce = 20000f;
        
        suspensionSpringRate = 30000f;
        suspensionDamper = 4000f;
        
        antiRollForce = 3000f;
        curveStabilization = 1.5f;
        
        ApplyGripSettings();
        ConfigureSuspension();
        
        Debug.Log("Car configured for testing with reduced values");
    }
    
    public void RestoreDefaultValues()
    {
        // Restaurar valores padrão das screenshots
        acceleration = 7000f;
        brakingForce = 2505600f;
        maxSteeringAngle = 30f;
        topSpeed = 64f;
        reverseSpeed = 40f;
        naturalResistance = 300f;
        
        gripLateralHigh = 8f;
        gripForwardHigh = 6f;
        extremumSlipLateralHigh = 0.3f;
        extremumSlipForwardHigh = 0.15f;
        asymptoteSlipHigh = 0.5f;
        asymptoteValueHigh = 3f;
        gripStiffness = 1.2f;
        
        downforce = 450f;
        maxDownforce = 50000f;
        
        suspensionSpringRate = 45000f;
        suspensionDamper = 6000f;
        suspensionDistance = 0.08f;
        
        antiRollForce = 4500f;
        curveStabilization = 2.0f;
        
        ApplyGripSettings();
        ConfigureSuspension();
        
        Debug.Log("Default values restored");
    }
    #endregion
}