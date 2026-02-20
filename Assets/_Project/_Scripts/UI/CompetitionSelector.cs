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
            FullTournamentManager.Instance.StartTournament(competitions[i]);
            break;
        }
    }

    public void LoadLastSave()
    {
        GameMode currentGameMode = GameManagerInstance.Instance.currentGameMode;
        switch (currentGameMode){
            case GameMode.Tournament:
                FullTournamentManager.Instance.LoadOldTournament();
                break;
        }
    }
}