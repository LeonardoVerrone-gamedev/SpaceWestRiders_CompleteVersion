using UnityEngine;
using UnityEngine.SceneManagement;

public class TitleScreenMenu : MonoBehaviour
{
    public GameObject[] gameModeOptions;

    //Scenes
    string quickRaceSceneName = "QuickRaceSetupScene";

    public void ShowGameOptions()
    {
        bool value = !gameModeOptions[0].activeInHierarchy ? true : false;

        foreach(GameObject gameModeOption in gameModeOptions) gameModeOption.SetActive(value);
    }

    public void StartQuickRaceMode()
    {
        SceneManager.LoadScene(quickRaceSceneName);
    }
}
