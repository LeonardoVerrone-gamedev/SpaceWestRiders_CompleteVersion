using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManagerInstance : MonoBehaviour
{
    public static GameManagerInstance Instance;

    public GameMode currentGameMode;

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

        //ForceDeleteAllTournamentSaves();
    }

    public void ForceDeleteAllTournamentSaves()
    {
        string folder = Application.persistentDataPath;

        var files = Directory.GetFiles(folder, "*.json");

        foreach (var file in files)
        {
            Debug.Log("Deleting: " + file);
            File.Delete(file);
        }
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

        if(scene.name == "TournamentCompetitionSelector" && currentGameMode != GameMode.StoryMode)
        {
            SetGameMode(GameMode.Tournament);
        }

        if(scene.name == "StoryCompetitionSelector")
        {
            SetGameMode(GameMode.StoryMode);
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
