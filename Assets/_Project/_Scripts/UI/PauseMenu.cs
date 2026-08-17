using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class PauseMenu : MonoBehaviour
{
    public static PauseMenu Instance { get; private set; }

    [Header("UI References")]
    [SerializeField] private GameObject rootPanel;
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button exitRaceButton;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    private void Start()
    {
        if (rootPanel != null)
            rootPanel.SetActive(false);

        if (resumeButton != null)
            resumeButton.onClick.AddListener(OnResumePressed);

        if (exitRaceButton != null)
            exitRaceButton.onClick.AddListener(OnExitRacePressed);
    }

    private void Update()
    {
        // Detecção direta via UnityEngine.InputSystem
        bool escPressed = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        bool startPressed = Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame;

        if (escPressed || startPressed)
        {
            PauseManager.Instance?.TogglePause();
        }
    }

    public void ShowUI(bool show)
    {
        if (rootPanel != null)
            rootPanel.SetActive(show);
    }

    public void OnResumePressed()
    {
        PauseManager.Instance?.ResumeGame();
    }

    public void OnExitRacePressed()
    {
        PauseManager.Instance?.ExitRace();
    }
}