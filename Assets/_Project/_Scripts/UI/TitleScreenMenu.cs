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
        SceneTransitionAnimationManager.Instance.LoadScene(quickRaceSceneName);
    }

    public void StartMiniTournament()
    {
        SceneTransitionAnimationManager.Instance.LoadScene(miniTournamentSceneName);
    }

    public void StartTournament()
    {
        SceneTransitionAnimationManager.Instance.LoadScene(TournamentScene);
    }
    
    public void StartStoryMode()
    {
        SceneTransitionAnimationManager.Instance.LoadScene(StoryModeScene);
    }
}
