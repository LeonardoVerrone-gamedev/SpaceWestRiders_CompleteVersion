using UnityEngine;
using System.Collections.Generic;

public class CompetitionSelector : MonoBehaviour
{
    public List<CompetitionSO> competitions;

    public void SelectCompetition(int i)
    {
        GameMode currentGameMode = GameManagerInstance.Instance.currentGameMode;
        switch (currentGameMode)
        {
            case GameMode.MiniTournament:
            MiniTournamentManager.Instance.StartTournament(competitions[i]);
            break;

            case GameMode.Tournament:
            TournamentFullManager.Instance.StartTournament(competitions[i]);
            break;
        }
    }
}