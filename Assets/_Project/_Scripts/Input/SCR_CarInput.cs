using UnityEngine;
using UnityEngine.InputSystem;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
using TouchPhase = UnityEngine.InputSystem.TouchPhase;
using System;

public enum InputMode
{
    MobileGyro,
    Keyboard,
    Gamepad,
    AI_Controlled
}

// Delegate para eventos de input
public delegate void SteeringChangedHandler(float steering);
public delegate void ThrottleChangedHandler(float throttle);
public delegate void BrakeChangedHandler(float brake);
public delegate void HandbrakeChangedHandler(bool handbrake);
public delegate void TurboPressedHandler(bool pressed);
public delegate void InputModeChangedHandler(InputMode newMode);
public delegate void SwitchCarPressHandler(bool pressed);

public class SCR_CarInput : MonoBehaviour
{
    [Header("Configuration")]
    [SerializeField] private InputMode _inputMode = InputMode.Keyboard;
    
    [Header("Mobile Gyro Settings")]
    [SerializeField] private float _maxTiltAngle = 45f;
    [SerializeField] private float _gyroSensitivity = 0.35f;
    [SerializeField] private AnimationCurve _gyroResponseCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    [SerializeField] private float _gyroDeadZone = 0.08f;
    [SerializeField] private float _gyroSmoothing = 0.15f;
    
    [Header("Touch Controls")]
    [SerializeField] private bool _enableTouchControls = true;
    [SerializeField] private float _swipeMinDistance = 50f;
    
    // Eventos públicos - outros scripts podem se inscrever
    public event SteeringChangedHandler OnSteeringChanged;
    public event ThrottleChangedHandler OnThrottleChanged;
    public event BrakeChangedHandler OnBrakeChanged;
    public event HandbrakeChangedHandler OnHandbrakeChanged;
    public event TurboPressedHandler OnTurboPressed;
    public event SwitchCarPressHandler OnSwitchMode;
    public event InputModeChangedHandler OnInputModeChanged;
    
    // Estado atual do input (apenas para debug/inspector)
    [Header("Current Input State (Read Only)")]
    [SerializeField] private float _currentSteering = 0f;
    [SerializeField] private float _currentThrottle = 0f;
    [SerializeField] private float _currentBrake = 0f;
    [SerializeField] private bool _currentHandbrake = false;
    
    // Mobile specific
    private Quaternion _gyroCenter;
    private bool _gyroCalibrated = false;
    private float _lastSteeringInput;
    
    // Touch tracking
    private Vector2 _touchStart;
    private bool _swipeDetected;
    
    // New Input System
    private PlayerInput _playerInput;
    private InputAction _steerAction;
    private InputAction _throttleAction;
    private InputAction _brakeAction;
    private InputAction _handbrakeAction;
    private InputAction _turboAction;
    private InputAction _toggleModeAction;
    
    // Estado anterior para otimização (evitar chamadas desnecessárias)
    private float _lastSteering = 0f;
    private float _lastThrottle = 0f;
    private float _lastBrake = 0f;
    private bool _lastHandbrake = false;
    private InputMode _lastInputMode;

    private bool _currentTurboPressed = false;
    private bool _lastTurboPressed = false;
    private bool _switchModeRequested = false;
    
    #region Unity Events
    
    private void Start()
    {
        InitializeInputSystem();
        
        // Enable Enhanced Touch for mobile
        if (_inputMode == InputMode.MobileGyro)
        {
            UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport.Enable();
        }
        
        _lastInputMode = _inputMode;
        
        if (_inputMode == InputMode.MobileGyro)
            InitializeMobileGyro();
    }
    
    private void Update()
    {
        // Atualizar estado do input
        UpdateInputState();
        
        // Disparar eventos se houver mudanças
        CheckAndDispatchEvents();
    }
    
    private void OnDestroy()
    {
        // Limpar todas as inscrições de eventos
        ClearAllEventSubscriptions();
    }
    #endregion
    
    #region Event System
    private void UpdateInputState()
    {
        switch (_inputMode)
        {
            case InputMode.MobileGyro:
                UpdateMobileInputState();
                break;
                
            case InputMode.Keyboard:
            case InputMode.Gamepad:
                UpdateDeviceInputState();
                break;
                
            case InputMode.AI_Controlled:
                // AI mantém estado manualmente via Set métodos
                break;
        }
    }
    
    private void CheckAndDispatchEvents()
    {
        // Verificar mudança de modo de input
        if (_lastInputMode != _inputMode)
        {
            OnInputModeChanged?.Invoke(_inputMode);
            _lastInputMode = _inputMode;
        }
        
        // Verificar mudanças e disparar eventos
        if (!Mathf.Approximately(_currentSteering, _lastSteering))
        {
            OnSteeringChanged?.Invoke(_currentSteering);
            _lastSteering = _currentSteering;
        }
        
        if (!Mathf.Approximately(_currentThrottle, _lastThrottle))
        {
            OnThrottleChanged?.Invoke(_currentThrottle);
            _lastThrottle = _currentThrottle;
        }
        
        if (!Mathf.Approximately(_currentBrake, _lastBrake))
        {
            OnBrakeChanged?.Invoke(_currentBrake);
            _lastBrake = _currentBrake;
        }
        
        if (_currentHandbrake != _lastHandbrake)
        {
            OnHandbrakeChanged?.Invoke(_currentHandbrake);
            _lastHandbrake = _currentHandbrake;
        }

        if (_currentTurboPressed != _lastTurboPressed)
        {
            OnTurboPressed?.Invoke(_currentTurboPressed);
            _lastTurboPressed = _currentTurboPressed;
        }

        if (_switchModeRequested)
        {
            OnSwitchMode?.Invoke(true);
            _switchModeRequested = false; // Consome o clique
        }
    }
    
    // Método para limpar todas as inscrições (útil ao trocar de cena)
    public void ClearAllEventSubscriptions()
    {
        OnSteeringChanged = null;
        OnThrottleChanged = null;
        OnBrakeChanged = null;
        OnHandbrakeChanged = null;
        OnTurboPressed = null;
        OnInputModeChanged = null;
    }
    #endregion
    
    #region Input Initialization
    private void InitializeInputSystem()
    {
        // Create default input actions if not using PlayerInput component
        if (TryGetComponent<PlayerInput>(out _playerInput))
        {
            // Bind to existing PlayerInput actions
            var actions = _playerInput.actions;
            _steerAction = actions["Steer"];
            _throttleAction = actions["Throttle"];
            _brakeAction = actions["Brake"];
            _handbrakeAction = actions["Handbrake"];
            _turboAction = actions["Turbo"];
            _toggleModeAction = actions["SwitchMode"];
        }

        BindActionsFromPlayerInput();
    }

    private void BindActionsFromPlayerInput()
    {
        var actions = _playerInput.actions;
        _steerAction = actions["Steer"];
        _throttleAction = actions["Throttle"];
        _brakeAction = actions["Brake"];
        _handbrakeAction = actions["Handbrake"];
        _turboAction = actions["Turbo"];
        _toggleModeAction = actions["SwitchMode"];
    }

    
    private void InitializeMobileGyro()
    {
        if (SystemInfo.supportsGyroscope)
        {
            Input.gyro.enabled = true;
            _gyroCenter = Quaternion.identity;
            _gyroCalibrated = true;
        }
    }
    #endregion
    
    #region Input Processing
    private void UpdateMobileInputState()
    {
        ProcessGyroSteering();
        
        if (_enableTouchControls)
        {
            ProcessTouchControls();
            DetectSwipe();
        }
    }
    
    private void UpdateDeviceInputState()
    {
        if (_steerAction != null)
            _currentSteering = _steerAction.ReadValue<float>();
            
        if (_throttleAction != null)
            _currentThrottle = _throttleAction.ReadValue<float>();
            
        if (_brakeAction != null)
            _currentBrake = _brakeAction.ReadValue<float>();
            
        if (_handbrakeAction != null)
            _currentHandbrake = _handbrakeAction.ReadValue<float>() > 0.5f;

        if (_turboAction != null){
            if (_turboAction.WasPressedThisFrame())
            {
                OnTurboPressed?.Invoke(true);
            }
        }

        if (_toggleModeAction != null)
        {
            // WasPressedThisFrame garante que só dispara uma vez por clique
            _switchModeRequested = _toggleModeAction.WasPressedThisFrame();
        }
    }
    
    private void ProcessGyroSteering()
    {
        if (!_gyroCalibrated || !SystemInfo.supportsGyroscope)
        {
            _currentSteering = 0f;
            return;
        }
        
        Quaternion deviceRotation = GyroToUnity(Input.gyro.attitude);
        Quaternion deltaRotation = Quaternion.Inverse(_gyroCenter) * deviceRotation;
        
        Vector3 euler = deltaRotation.eulerAngles;
        float zAngle = NormalizeAngle(euler.z);
        
        float rawInput = zAngle / _maxTiltAngle;
        
        // Apply deadzone
        if (Mathf.Abs(rawInput) < _gyroDeadZone)
        {
            rawInput = 0f;
        }
        else
        {
            float sign = Mathf.Sign(rawInput);
            float magnitude = (Mathf.Abs(rawInput) - _gyroDeadZone) / (1f - _gyroDeadZone);
            rawInput = sign * magnitude;
            
            // Apply response curve
            float curvedInput = _gyroResponseCurve.Evaluate(Mathf.Abs(rawInput)) * sign;
            rawInput = curvedInput * _gyroSensitivity;
        }
        
        rawInput = Mathf.Clamp(rawInput, -1f, 1f);
        
        // Smoothing
        if (_gyroSmoothing > 0)
        {
            rawInput = Mathf.Lerp(_lastSteeringInput, rawInput, 1f - _gyroSmoothing);
            _lastSteeringInput = rawInput;
        }
        
        _currentSteering = -rawInput;
    }
    
    private void ProcessTouchControls()
    {
        _currentThrottle = 0f;
        _currentBrake = 0f;
        
        // Use Enhanced Touch API
        if (Touch.activeTouches.Count > 0)
        {
            // Calibrate on first touch
            if (!_gyroCalibrated)
            {
                _gyroCenter = GyroToUnity(Input.gyro.attitude);
                _gyroCalibrated = true;
            }
            
            foreach (var touch in Touch.activeTouches)
            {
                if (touch.phase == TouchPhase.Began || 
                    touch.phase == TouchPhase.Moved || 
                    touch.phase == TouchPhase.Stationary)
                {
                    Vector2 pos = touch.screenPosition;
                    
                    if (pos.x > Screen.width * 0.6f)
                    {
                        _currentThrottle = 1f;
                    }
                    else if (pos.x < Screen.width * 0.4f)
                    {
                        _currentBrake = 1f;
                    }
                }
            }
        }
    }
    
    private void DetectSwipe()
    {
        if (Touch.activeTouches.Count == 0) return;
        
        var touch = Touch.activeTouches[0];
        
        if (touch.phase == TouchPhase.Began)
        {
            _touchStart = touch.screenPosition;
            _swipeDetected = true;
        }
        else if (touch.phase == TouchPhase.Ended && _swipeDetected)
        {
            Vector2 end = touch.screenPosition;
            Vector2 delta = end - _touchStart;
            
            if (Mathf.Abs(delta.y) > _swipeMinDistance && 
                Mathf.Abs(delta.y) > Mathf.Abs(delta.x))
            {
                // Swipe down = turbo
                OnTurboPressed?.Invoke(delta.y < 0);
            }
            
            _swipeDetected = false;
        }
    }
    #endregion
    
    #region Public Methods for AI/External Control
    public void SetInputMode(InputMode mode) 
    { 
        _inputMode = mode;
        
        // Disable Enhanced Touch if switching away from mobile
        if (mode != InputMode.MobileGyro && UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport.enabled)
        {
            UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport.Disable();
        }
        else if (mode == InputMode.MobileGyro && !UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport.enabled)
        {
            UnityEngine.InputSystem.EnhancedTouch.EnhancedTouchSupport.Enable();
        }
    }
    
    // Métodos para AI controlar o carro diretamente
    public void SetSteeringInput(float input) 
    { 
        _currentSteering = Mathf.Clamp(input, -1f, 1f);
    }
    
    public void SetThrottleInput(float input) 
    { 
        _currentThrottle = Mathf.Clamp(input, -1f, 1f);
    }
    
    public void SetBrakeInput(float input) 
    { 
        _currentBrake = Mathf.Clamp01(input);
    }
    
    public void SetHandbrakeInput(bool input) 
    { 
        _currentHandbrake = input;
    }
    
    public void TriggerTurbo(bool activate)
    {
        OnTurboPressed?.Invoke(activate);
    }
    
    // Métodos completos para controle AI (incluem eventos)
    public void SetSteeringInputWithEvent(float input)
    {
        SetSteeringInput(input);
        OnSteeringChanged?.Invoke(_currentSteering);
    }
    
    public void SetThrottleInputWithEvent(float input)
    {
        SetThrottleInput(input);
        OnThrottleChanged?.Invoke(_currentThrottle);
    }
    
    public void SetBrakeInputWithEvent(float input)
    {
        SetBrakeInput(input);
        OnBrakeChanged?.Invoke(_currentBrake);
    }
    
    public void SetHandbrakeInputWithEvent(bool input)
    {
        SetHandbrakeInput(input);
        OnHandbrakeChanged?.Invoke(_currentHandbrake);
    }
    #endregion
    
    #region Convenience Methods
    // Método para configurar tudo de uma vez (útil para AI)
    public void SetAllInputs(float steering, float throttle, float brake, bool handbrake)
    {
        bool steeringChanged = !Mathf.Approximately(steering, _currentSteering);
        bool throttleChanged = !Mathf.Approximately(throttle, _currentThrottle);
        bool brakeChanged = !Mathf.Approximately(brake, _currentBrake);
        bool handbrakeChanged = handbrake != _currentHandbrake;
        
        SetSteeringInput(steering);
        SetThrottleInput(throttle);
        SetBrakeInput(brake);
        SetHandbrakeInput(handbrake);
        
        // Disparar eventos apenas se houver mudanças
        if (steeringChanged) OnSteeringChanged?.Invoke(_currentSteering);
        if (throttleChanged) OnThrottleChanged?.Invoke(_currentThrottle);
        if (brakeChanged) OnBrakeChanged?.Invoke(_currentBrake);
        if (handbrakeChanged) OnHandbrakeChanged?.Invoke(_currentHandbrake);
    }
    
    // Método para obter estado atual como struct (útil para salvar/replay)
    public CarInputState GetCurrentInputState()
    {
        return new CarInputState
        {
            steering = _currentSteering,
            throttle = _currentThrottle,
            brake = _currentBrake,
            handbrake = _currentHandbrake,
            timestamp = Time.time
        };
    }
    #endregion

    #region gamepad management

    public Gamepad GetAssignedGamepad()
    {
        if (_playerInput == null)
            return null;

        foreach (var device in _playerInput.devices)
        {
            if (device is Gamepad gamepad)
                return gamepad;
        }

        return null;
    }

    #endregion

    #region Input Refreshing
    /// <summary>
    /// Re-vincula as referências das InputActions do PlayerInput.
    /// Chamado pelo Manager após habilitar o componente e parear o dispositivo.
    /// </summary>
    public void RefreshInputActions()
    {
        if (TryGetComponent<PlayerInput>(out _playerInput))
        {
            var actions = _playerInput.actions;

            // 2. Re-vincular referências das ações
            _steerAction = actions.FindAction("Steer");
            _throttleAction = actions.FindAction("Throttle");
            _brakeAction = actions.FindAction("Brake");
            _handbrakeAction = actions.FindAction("Handbrake");
            _turboAction = actions.FindAction("Turbo");
            _toggleModeAction = actions.FindAction("SwitchMode");

            Debug.Log($"[SCR_CarInput] Ações de Input atualizadas para: {gameObject.name}");
        }
    }
    #endregion
    
    
    #region Gyro Utilities
    private Quaternion GyroToUnity(Quaternion q) => new Quaternion(q.x, q.y, -q.z, -q.w);
    
    private float NormalizeAngle(float angle)
    {
        while (angle > 180f) angle -= 360f;
        while (angle < -180f) angle += 360f;
        return angle;
    }
    
    public void RecalibrateGyro()
    {
        if (SystemInfo.supportsGyroscope)
        {
            _gyroCenter = GyroToUnity(Input.gyro.attitude);
            _gyroCalibrated = true;
        }
    }
    #endregion
    
    #region Inspector Helpers & Properties
    public InputMode CurrentInputMode => _inputMode;
    
    // Propriedades de estado atual (apenas leitura)
    public float CurrentSteering => _currentSteering;
    public float CurrentThrottle => _currentThrottle;
    public float CurrentBrake => _currentBrake;
    public bool CurrentHandbrake => _currentHandbrake;
    
    [ContextMenu("Switch to Keyboard")]
    private void SwitchToKeyboard() => SetInputMode(InputMode.Keyboard);
    
    [ContextMenu("Switch to Gamepad")]
    private void SwitchToGamepad() => SetInputMode(InputMode.Gamepad);
    
    [ContextMenu("Switch to Mobile")]
    private void SwitchToMobile() => SetInputMode(InputMode.MobileGyro);
    
    [ContextMenu("Switch to AI")]
    private void SwitchToAI() => SetInputMode(InputMode.AI_Controlled);
    #endregion
}

// Struct para representar estado completo do input
[System.Serializable]
public struct CarInputState
{
    public float steering;
    public float throttle;
    public float brake;
    public bool handbrake;
    public float timestamp;
    
    public override string ToString()
    {
        return $"Steering: {steering:F2}, Throttle: {throttle:F2}, Brake: {brake:F2}, Handbrake: {handbrake}";
    }
}
