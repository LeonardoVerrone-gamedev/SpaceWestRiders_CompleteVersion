
using System;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManagerInstance : MonoBehaviour
{
    public static GameManagerInstance Instance;

    public GameMode currentGameMode;

    // Identificador persistente desta instalação.
    public const string PLAYER_ID_KEY = "PlayerId";

    public string PlayerId { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;

            DontDestroyOnLoad(gameObject);

            // Garante que a instalação tenha um UUID válido.
            InitializePlayerId();

            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        else
        {
            Destroy(gameObject);
        }

        //ForceDeleteAllTournamentSaves();
    }

    private void InitializePlayerId()
    {
        string savedID = PlayerPrefs.GetString(
            PLAYER_ID_KEY,
            ""
        );

        // Reutiliza o ID existente se for um UUID válido.
        if (Guid.TryParse(savedID, out Guid existingGuid))
        {
            PlayerId = existingGuid.ToString();

            Debug.Log(
                $"[PLAYER ID] ID existente carregado: {PlayerId}"
            );

            return;
        }

        // Gera um novo UUID para esta instalação.
        PlayerId = Guid.NewGuid().ToString();

        PlayerPrefs.SetString(
            PLAYER_ID_KEY,
            PlayerId
        );

        PlayerPrefs.Save();

        Debug.Log(
            $"[PLAYER ID] Novo ID gerado: {PlayerId}"
        );
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

        if (scene.name == "MiniTournamentCompetitionSelector")
        {
            SetGameMode(GameMode.MiniTournament);
        }

        if (scene.name == "TournamentCompetitionSelector" &&
            currentGameMode != GameMode.StoryMode)
        {
            SetGameMode(GameMode.Tournament);
        }

        if (scene.name == "StoryCompetitionSelector")
        {
            SetGameMode(GameMode.StoryMode);
        }

        if (scene.name == "QuickRaceSetupScene")
        {
            SetGameMode(GameMode.QuickPlay);
        }
    }

    void OnDisable()
    {
        if (Instance == this)
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }
    }

    void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
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
