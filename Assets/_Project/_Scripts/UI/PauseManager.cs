using UnityEngine;

public class PauseManager : MonoBehaviour
{
    public static PauseManager Instance { get; private set; }

    public bool IsPaused { get; private set; } = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    public void TogglePause()
    {
        if (IsPaused)
            ResumeGame();
        else
            PauseGame();
    }

    public void PauseGame()
    {
        if(RankingUI.Instance.OnRanking())return;
        IsPaused = true;
        Time.timeScale = 0f;
        PauseMenu.Instance?.ShowUI(true);
    }

    public void ResumeGame()
    {
        IsPaused = false;
        Time.timeScale = 1f;
        PauseMenu.Instance?.ShowUI(false);
    }

    public void ExitRace()
    {
        // Garante que o tempo volte ao normal antes de mudar de cena
        Time.timeScale = 1f;
        IsPaused = false;

        GameMode currentMode = GameManagerInstance.Instance != null 
            ? GameManagerInstance.Instance.currentGameMode 
            : GameMode.QuickPlay;

        switch (currentMode)
        {
            case GameMode.QuickPlay:
                QuickPlayManagement.Instance?.ReturnToMenu();
                break;

            case GameMode.MiniTournament:
                if (GameManagerInstance.Instance?.currentGameMode != GameMode.MiniTournament) return;
                SceneTransitionAnimationManager.Instance.LoadScene("MiniTournamentMainMenu");
                break;

            case GameMode.Tournament:
                if (GameManagerInstance.Instance?.currentGameMode != GameMode.Tournament && GameManagerInstance.Instance?.currentGameMode != GameMode.StoryMode) return;
                SceneTransitionAnimationManager.Instance.LoadScene("TournamentMainMenu");
                break;

            case GameMode.StoryMode:
                FullTournamentManager.Instance?.SaveAndQuit();
                break;

            default:
                Debug.LogWarning("Modo de jogo não reconhecido ao tentar sair da corrida.");
                break;
        }
    }
}