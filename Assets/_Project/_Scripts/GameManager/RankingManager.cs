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

    Dictionary<RacerStatus, int> CalculatePoints(List<RacerStatus> ranking)
    {
        Dictionary<RacerStatus, int> results = new();

        for (int i = 0; i < ranking.Count; i++)
        {
            int position = i + 1;
            results.Add(ranking[i], GetPoints(position));
        }

        return results;
    }

    int GetPoints(int position)
    {
        return position switch
        {
            1 => 10,
            2 => 6,
            3 => 5,
            4 => 4,
            5 => 3,
            6 => 2,
            7 => 1,
            _ => 0
        };
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
}