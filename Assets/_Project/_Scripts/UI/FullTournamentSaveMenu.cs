using UnityEngine;

public class FullTournamentSaveMenu : MonoBehaviour
{
    [SerializeField] GameObject SaveSystemPanel;
    void Start()
    {
        checkSaveState();
    }

    private void checkSaveState()
    {
        if (FullTournamentManager.Instance.CheckFile())
        {
            SaveSystemPanel.SetActive(true);
        }else SaveSystemPanel.SetActive(false);
    }

    public void OnLoadButton()
    {
        FullTournamentManager.Instance.LoadIfExists();
        SaveSystemPanel.SetActive(false);
    }

    public void OnDeleteSaveButton()
    {
        FullTournamentManager.Instance.DeleteSave();
        SaveSystemPanel.SetActive(false);
    }
}