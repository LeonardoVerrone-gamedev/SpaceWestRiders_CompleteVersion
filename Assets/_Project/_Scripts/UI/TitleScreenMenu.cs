using UnityEngine;
using UnityEngine.SceneManagement;

public class TitleScreenMenu : MonoBehaviour
{
    public GameObject[] gameModeOptions;

    //Scenes
    string quickRaceSceneName = "QuickRaceSetupScene";
    string miniTournamentSceneName = "MiniTournamentCompetitionSelector";
    string TournamentScene = "TournamentCompetitionSelector";
    string StoryModeScene = "StoryCompetitionSelector";

    public void ShowGameOptions()
    {
        bool value = !gameModeOptions[0].activeInHierarchy ? true : false;

        foreach(GameObject gameModeOption in gameModeOptions) gameModeOption.SetActive(value);
    }

    public void StartQuickRaceMode()
    {
        SceneManager.LoadScene(quickRaceSceneName);
    }

    public void StartMiniTournament()
    {
        SceneManager.LoadScene(miniTournamentSceneName);
    }

    public void StartTournament()
    {
        SceneManager.LoadScene(TournamentScene);
    }
    
    public void StartStoryMode()
    {
        SceneManager.LoadScene(StoryModeScene);
    }
}
