using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class RankingManager : MonoBehaviour
{
    public static RankingManager Instance;

    [SerializeField] private RankingUI rankingUI;

    private bool rankingOpened = false;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void OpenRanking(List<RaceResultData> results)
    {
        if (rankingOpened) return;

        rankingOpened = true;
        rankingUI.Open(results);
    }

    public void CloseRanking()
    {
        rankingOpened = false;
    }


    // ==========================
    // BOTÕES - QUICK PLAY (chamam QuickPlayManagement diretamente)
    // ==========================

    public void TryAgain()
    {
        if (GameManagerInstance.Instance?.currentGameMode != GameMode.QuickPlay) return;
        QuickPlayManagement.Instance?.TryAgain();
    }

    public void Continue()
    {
        if (GameManagerInstance.Instance?.currentGameMode != GameMode.QuickPlay) return;
        QuickPlayManagement.Instance?.ReturnToMenu();
    }

    public void TryAnotherCircuit()
    {
        if (GameManagerInstance.Instance?.currentGameMode != GameMode.QuickPlay) return;
        QuickPlayManagement.Instance?.TryAnotherCircuit();
    }

    // ==========================
    // BOTÕES - MINI TOURNAMENT (chamam MiniTournamentManager diretamente)
    // ==========================

    public void ContinueToNextRace()
    {
        if (GameManagerInstance.Instance?.currentGameMode != GameMode.MiniTournament) return;
        MiniTournamentManager.Instance?.AdvanceToNextRace();
    }

    public void BackToTournamentMenu()
    {
        if (GameManagerInstance.Instance?.currentGameMode != GameMode.MiniTournament) return;
        SceneManager.LoadScene("MiniTournamentMainMenu");
    }

    public void GiveUp()
    {
        if (GameManagerInstance.Instance?.currentGameMode != GameMode.MiniTournament) return;
        MiniTournamentManager.Instance?.EndTournament();
    }

    // ==========================
    // BOTÕES - FULL TOURNAMENT (chamam FullTournamentManager diretamente)
    // ==========================

    public void ContinueFullTournament()
    {
        if (GameManagerInstance.Instance?.currentGameMode != GameMode.Tournament) return;
        FullTournamentManager.Instance?.AdvanceToNextRace();
    }

    public void BackToFullTournamentMenu()
    {
        if (GameManagerInstance.Instance?.currentGameMode != GameMode.Tournament) return;
        SceneManager.LoadScene("TournamentMainMenu");
    }

    public void SaveAndQuitFullTournament()
    {
        if (GameManagerInstance.Instance?.currentGameMode != GameMode.Tournament) return;
        FullTournamentManager.Instance?.SaveAndQuit();
    }

}