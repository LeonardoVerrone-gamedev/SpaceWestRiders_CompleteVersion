using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManagerInstance : MonoBehaviour
{
    public static GameManagerInstance Instance;

    public GameMode currentGameMode {get; private set;}

    void Awake(){
        if(GameManagerInstance.Instance == null) 
        {
            Instance = this;
            DontDestroyOnLoad(this);
        }
        else
        {
            Destroy(this);
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "TitleScreen")
        {
            SetGameMode(GameMode.None);
        }

        if(scene.name == "MiniTournamentCompetitionSelector")
        {
            SetGameMode(GameMode.MiniTournament);
        }
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    public void SetGameMode(GameMode gameMode)
    {
        currentGameMode = gameMode;
    }
}

public enum GameMode
{
    None,
    QuickPlay,
    MiniTournament,
    Tournament,
    StoryMode
}
