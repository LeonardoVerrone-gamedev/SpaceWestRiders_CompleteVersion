using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class CompetitionSelector : MonoBehaviour
{
    [Header("Competitions")]
    public List<CompetitionSO> competitions;

    [Header("Tournament Buttons (for story mode)")]
    [SerializeField] private List<Button> competitionButtons;

    private void Start()
    {
        if (GameManagerInstance.Instance.currentGameMode == GameMode.StoryMode)
        {
            SetupStoryModeButtons();
        }
    }

    // ------------------------------------------------------

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

             case GameMode.StoryMode:
                // Verifica se já existe um save para este torneio
                StoryModeManager.Instance.SetTournamentIndex(i);
                
                if (FullTournamentManager.Instance.CheckFile())
                {
                    // Se existe save, carrega
                    FullTournamentManager.Instance.LoadOldTournament();
                }
                else
                {
                    // Se não existe, começa novo
                    FullTournamentManager.Instance.StartTournament(competitions[i]);
                }
                break;
        }
    }

    // ------------------------------------------------------

    public void LoadLastSave()
    {
        GameMode currentGameMode = GameManagerInstance.Instance.currentGameMode;

        switch (currentGameMode)
        {
            case GameMode.Tournament:
                FullTournamentManager.Instance.LoadOldTournament();
                break;

            case GameMode.StoryMode:
                StoryModeManager.Instance.StartStoryMode();
                break;
        }
    }

    // ------------------------------------------------------

    private void SetupStoryModeButtons()
    {
        if (competitionButtons == null || competitionButtons.Count == 0)
            return;

        for (int i = 0; i < competitionButtons.Count; i++)
        {
            bool unlocked = StoryModeManager.Instance.IsTournamentUnlocked(i);
            competitionButtons[i].interactable = unlocked;
        }
    }
}