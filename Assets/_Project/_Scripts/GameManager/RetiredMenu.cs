using UnityEngine;
using UnityEngine.UI;

public class RetireScreenUIManager : MonoBehaviour
{
    public static RetireScreenUIManager Instance;

    [Header("UI")]
    [SerializeField] private GameObject rootPanel;

    [Header("Buttons")]
    [SerializeField] private Button retryButton;
    [SerializeField] private Button quitButton;

    private bool screenOpened = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    void Start()
    {
        if (rootPanel != null)
            rootPanel.SetActive(false);

        ConfigureButtons();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // ==========================
    // CONFIGURAÇÃO
    // ==========================

    private void ConfigureButtons()
    {
        bool isQuickPlay =
            GameManagerInstance.Instance?.currentGameMode == GameMode.QuickPlay;

        // Retry só existe no Quick Play
        if (retryButton != null)
            retryButton.gameObject.SetActive(isQuickPlay);
    }

    // ==========================
    // SCREEN
    // ==========================

    public void Open()
    {
        if (screenOpened) return;

        screenOpened = true;

        ConfigureButtons();

        if (rootPanel != null)
            rootPanel.SetActive(true);
    }

    public void Close()
    {
        screenOpened = false;

        if (rootPanel != null)
            rootPanel.SetActive(false);
    }

    public bool IsOpen()
    {
        return screenOpened;
    }

    // ==========================
    // RETRY
    // ==========================

    public void Retry()
    {
        if (GameManagerInstance.Instance?.currentGameMode != GameMode.QuickPlay)
            return;

        QuickPlayManagement.Instance?.TryAgain();
    }

    // ==========================
    // QUIT
    // ==========================

    public void Quit()
    {
        if (GameManagerInstance.Instance?.currentGameMode == GameMode.QuickPlay)
        {
            QuickPlayManagement.Instance?.ReturnToMenu();
        }
        else if (GameManagerInstance.Instance?.currentGameMode == GameMode.MiniTournament)
        {
            MiniTournamentManager.Instance?.EndTournament();
        }
        else if (GameManagerInstance.Instance?.currentGameMode == GameMode.Tournament ||
                 GameManagerInstance.Instance?.currentGameMode == GameMode.StoryMode)
        {
            FullTournamentManager.Instance?.SaveAndQuit();
        }
    }
}