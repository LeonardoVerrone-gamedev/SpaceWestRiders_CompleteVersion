using System.Collections.Generic;
using UnityEngine;

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
    // BOTÕES
    // ==========================

    public void TryAgain()
    {
        QuickPlayManagement.Instance?.TryAgain();
    }

    public void Continue()
    {
        QuickPlayManagement.Instance?.ReturnToMenu();
    }

    public void TryAnotherCircuit()
    {
        QuickPlayManagement.Instance?.TryAnotherCircuit();
    }
}
