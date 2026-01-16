using UnityEngine;

public enum VehiclePhysicsType { Grounded, Hover, LegacyWheel }

public class SCR_PhysicsConnector : MonoBehaviour
{
    [Header("Input System")]
    [SerializeField] private SCR_CarInput _carInput;

    [Header("Physics Modules")]
    [SerializeField] private SCR_RayBasedCarPhysics _rayPhysics;

    [Header("Settings")]
    [SerializeField] private bool _autoFind = true;

    private VehiclePhysicsType _currentMode;

    private void Awake()
    {
        if (_autoFind) FindComponents();
    }

    private void OnEnable()
    {
        ConnectToInputEvents();
    }

    private void OnDisable()
    {
        DisconnectFromInputEvents();
    }

    private void FindComponents()
    {
        if (!_carInput) _carInput = GetComponent<SCR_CarInput>();
        if (!_rayPhysics) _rayPhysics = GetComponent<SCR_RayBasedCarPhysics>();

        _carInput.enabled = true;
    }

    #region Mode Switching (O Coração do sistema híbrido)

    /// <summary>
    /// Troca o modo de física ativando um componente e desativando os outros.
    /// Ideal para transformações estilo DeLorean.
    /// </summary>
    public void SetPhysicsMode(VehiclePhysicsType newMode)
    {
        _currentMode = newMode;

        // Desabilita todos primeiro (Garante que não haverá conflito de forças no Rigidbody)
        if (_rayPhysics) _rayPhysics.enabled = false;

        if (_rayPhysics) _rayPhysics.enabled = true;

        ResetInputs();
        Debug.Log($"[PhysicsConnector] Switched to {newMode} mode.");
    }

    // Atalhos rápidos para Invokes ou Botões da UI
    public void SwitchToHover() => SetPhysicsMode(VehiclePhysicsType.Hover);
    public void SwitchToGrounded() => SetPhysicsMode(VehiclePhysicsType.Grounded);

    #endregion

    #region Input Event Connection
    private void ConnectToInputEvents()
    {
        if (!_carInput) return;
        _carInput.OnSteeringChanged += OnSteer;
        _carInput.OnThrottleChanged += OnThrottle;
        _carInput.OnBrakeChanged += OnBrake;
        _carInput.OnHandbrakeChanged += OnHandbrake;
        _carInput.OnTurboPressed += OnTurbo;
        _carInput.OnSwitchMode += OnSwitchMode;
    }

    private void DisconnectFromInputEvents()
    {
        if (!_carInput) return;
        _carInput.OnSteeringChanged -= OnSteer;
        _carInput.OnThrottleChanged -= OnThrottle;
        _carInput.OnBrakeChanged -= OnBrake;
        _carInput.OnHandbrakeChanged -= OnHandbrake;
        _carInput.OnTurboPressed -= OnTurbo;
        _carInput.OnSwitchMode -= OnSwitchMode;
    }
    #endregion

    #region Event Handlers (Otimizados)
    
    // Agora enviamos o comando apenas para quem estiver ativo (enabled)
    // Isso evita dezenas de checagens de null por frame.

    private void OnSteer(float value)
    {
        if (_rayPhysics && _rayPhysics.enabled) _rayPhysics.SetSteering(value);
    }

    private void OnThrottle(float value)
    {
        if (_rayPhysics && _rayPhysics.enabled) _rayPhysics.SetThrottle(value);
    }

    private void OnBrake(float value)
    {
        if (_rayPhysics && _rayPhysics.enabled) _rayPhysics.SetBrake(value);
    }

    private void OnHandbrake(bool value)
    {
        if (_rayPhysics && _rayPhysics.enabled) _rayPhysics.SetHandbrake(value);
    }

    private void OnTurbo(bool value)
    {
        if (value)
        {
            if (_rayPhysics && _rayPhysics.enabled) _rayPhysics.ActivateTurbo();
        }
        else
        {
            if (_rayPhysics && _rayPhysics.enabled) _rayPhysics.DeactivateTurbo();
        }
    }

    private void OnSwitchMode(bool value)
    {
        if (_rayPhysics && _rayPhysics.enabled) _rayPhysics.ToggleVehicleMode();
    }
    #endregion

    private void ResetInputs()
    {
        OnSteer(0);
        OnThrottle(0);
        OnBrake(0);
        OnHandbrake(false);
    }
}