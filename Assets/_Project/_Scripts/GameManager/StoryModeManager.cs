using UnityEngine;
using UnityEngine.SceneManagement;
using System.IO;
using System.Collections.Generic;

public class StoryModeManager : MonoBehaviour
{
    public static StoryModeManager Instance;

    [Header("Story Tournaments (Ordem fixa)")]
    [SerializeField] public List<CompetitionSO> storyTournaments;

    private string SavePath => 
        Path.Combine(Application.persistentDataPath, "story_mode_save.json");

    public StoryModeState CurrentState { get; private set; }

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            LoadIfExists();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // ---------------------------------------------------

    public void StartStoryMode()
    {
        GameManagerInstance.Instance.SetGameMode(GameMode.StoryMode);

        if (CurrentState == null)
            CreateNewStory();

        LoadCurrentTournament();
    }

    void CreateNewStory()
    {
        CurrentState = new StoryModeState();
        CurrentState.currentTournamentIndex = 0;
        CurrentState.completedTournaments = new bool[storyTournaments.Count];

        Save();
    }

    public void LoadCurrentTournament()
    {
        int index = CurrentState.currentTournamentIndex;

        var fullManager = FullTournamentManager.Instance;

        if (fullManager.CheckFile())
        {
            fullManager.LoadOldTournament();
        }
        else
        {
            fullManager.StartTournament(storyTournaments[index]);
        }
    }

    public void OnTournamentCompleted()
    {
        int index = CurrentState.currentTournamentIndex;

        CurrentState.completedTournaments[index] = true;

        if (index < storyTournaments.Count - 1)
        {
            CurrentState.currentTournamentIndex++;
            Save();
            LoadCurrentTournament();
        }
        else
        {
            Debug.Log("STORY MODE COMPLETED!");
            EndStoryMode();
        }
    }

    void EndStoryMode()
    {
        DeleteSave();
        SceneTransitionAnimationManager.Instance.LoadScene("TitleScreen");
    }//

    // ---------------------------------------------------

    void Save()
    {
        string json = JsonUtility.ToJson(CurrentState, true);
        File.WriteAllText(SavePath, json);
    }

    void LoadIfExists()
    {
        if (!File.Exists(SavePath)) return;

        string json = File.ReadAllText(SavePath);
        CurrentState = JsonUtility.FromJson<StoryModeState>(json);

        ValidateState();
    }

    void ValidateState()
    {
        if (CurrentState.completedTournaments == null 
            || CurrentState.completedTournaments.Length != storyTournaments.Count)
        {
            bool[] newArray = new bool[storyTournaments.Count];

            if (CurrentState.completedTournaments != null)
            {
                for (int i = 0; i < Mathf.Min(
                    CurrentState.completedTournaments.Length,
                    newArray.Length); i++)
                {
                    newArray[i] = CurrentState.completedTournaments[i];
                }
            }

            CurrentState.completedTournaments = newArray;
            Save();
        }
    }

    public void DeleteSave()
    {
        if (File.Exists(SavePath))
            File.Delete(SavePath);
    }

   public bool IsTournamentUnlocked(int index)
    {
        if (CurrentState == null)
            return index == 0;

        if (index == 0) return true;

        if (index - 1 >= CurrentState.completedTournaments.Length)
            return false;

        return CurrentState.completedTournaments[index - 1];
    }

    public void SetTournamentIndex(int index)
    {
        if (CurrentState == null)
            CreateNewStory();

        CurrentState.currentTournamentIndex = index;
        Save();
    }
}

[System.Serializable]
public class StoryModeState
{
    public int currentTournamentIndex;
    public bool[] completedTournaments;
}