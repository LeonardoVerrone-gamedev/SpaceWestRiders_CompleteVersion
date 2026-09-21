using UnityEngine;

/// <summary>
/// Inicializa um carro especial como IA de corrida sem passar pelo
/// SCR_TrackSelectionManager.
///
/// Esse carro:
/// - Não é jogador.
/// - Não recebe câmera.
/// - Não participa da seleção.
/// - Não precisa estar no PersistentData.
/// - Usa o mesmo AIRacingController dos demais corredores.
/// </summary>
[DisallowMultipleComponent]
public class SCR_CameramanAI : MonoBehaviour
{
    [Header("AI")]
    [SerializeField] private bool enableAIOnAwake = true;

    [Header("Race")]
    [SerializeField] private bool registerRigidbodyInRaceManager = false;

    private SCR_CarInput carInput;
    private AIRacingController ai;
    private Rigidbody rb;

    public static SCR_CameramanAI Instance;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        Initialize();
    }


    public void killCameraman()
    {
        Destroy(gameObject);
    }

    private void Initialize()
    {
        carInput = GetComponent<SCR_CarInput>();
        ai = GetComponent<AIRacingController>();
        rb = GetComponent<Rigidbody>();

        if (carInput == null)
        {
            Debug.LogError(
                $"[{nameof(SCR_CameramanAI)}] " +
                $"'{gameObject.name}' não possui SCR_CarInput.",
                this
            );

            return;
        }

        // =========================================================
        // INPUT
        // =========================================================

        // Esse carro nunca será controlado por PlayerInput.
        // Portanto, garante que esteja no modo usado pelo AIRacingController.
        carInput.SetInputMode(InputMode.AI_Controlled);

        // Se alguém colocou PlayerInput no prefab por engano,
        // ele não deve interferir no Cameraman.
        if (TryGetComponent<UnityEngine.InputSystem.PlayerInput>(
                out var playerInput))
        {
            playerInput.enabled = false;
        }

        // =========================================================
        // AI
        // =========================================================

        if (enableAIOnAwake)
        {
            if (ai == null)
            {
                ai = gameObject.AddComponent<AIRacingController>();
            }

            ai.enabled = true;
        }

        // =========================================================
        // RIGIDBODY
        // =========================================================

        if (rb != null && registerRigidbodyInRaceManager)
        {
            RegisterRigidbodyForRace();
        }
    }

    private void RegisterRigidbodyForRace()
    {
        if (RaceManager.Instance == null)
        {
            Debug.LogWarning(
                $"[{nameof(SCR_CameramanAI)}] " +
                $"RaceManager ainda não existe para '{gameObject.name}'. " +
                $"O Rigidbody não foi registrado.",
                this
            );

            return;
        }

        RaceManager.Instance.AddRigidbodyForRaceStart(rb);
    }
}