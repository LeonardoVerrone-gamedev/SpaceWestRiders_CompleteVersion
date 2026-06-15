using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Text;
using System;

public class RankingUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject rootPanel;
    [SerializeField] private TextMeshProUGUI rankText;
    
    [Header("QUICK PLAY Buttons")]
    [SerializeField] private Button tryAgainButton;        // Quick Play: tentar novamente
    [SerializeField] private Button continueButton;        // Quick Play: continuar, retorna para title screen
    [SerializeField] private Button tryAnotherCircuitButton; // Quick Play: outro circuito

    [Header("MINI TOURNAMENT BUTTONS")]
    [SerializeField] private Button backToMiniTournamentMenuButton; // Torneio: voltar ao menu de torneio
    [SerializeField] private Button backToTitleButton;              // Torneio: desistir, voltar a title screen
    [SerializeField] private Button continueToNextRaceButton;       // Torneio: avançar para proxima corrida

    [Header("FULL TOURNAMENT BUTTONS")]
    [SerializeField] private Button backToFullTournamentMenuButton;
    [SerializeField] private Button backToTitleFullButton;
    [SerializeField] private Button continueFullTournamentButton;


    [Header("Button Labels")]
    [SerializeField] private TextMeshProUGUI continueToNextRaceLabel;

    private List<RaceResultData> currentResults;

    void Awake()
    {
        // Configura botões baseado no modo de jogo
        ConfigureButtonsForGameMode();
    }

    void Start()
    {
        rootPanel.SetActive(false);
    }

    private void ConfigureButtonsForGameMode()
    {
        bool isQuickRace = (GameManagerInstance.Instance?.currentGameMode == GameMode.QuickPlay);
        bool isTournament = (GameManagerInstance.Instance?.currentGameMode == GameMode.MiniTournament);
        bool isFull = (GameManagerInstance.Instance?.currentGameMode == GameMode.Tournament || GameManagerInstance.Instance?.currentGameMode == GameMode.StoryMode);

        // Quick Play: mostra tryAgain, continue, tryAnotherCircuit
        if (tryAgainButton != null)
            tryAgainButton.gameObject.SetActive(isQuickRace);

        if (continueButton != null)
            continueButton.gameObject.SetActive(isQuickRace);

        if (tryAnotherCircuitButton != null)
            tryAnotherCircuitButton.gameObject.SetActive(isQuickRace);

        // Torneio: mostra os botões específicos
        if (backToMiniTournamentMenuButton != null)
            backToMiniTournamentMenuButton.gameObject.SetActive(isTournament);

        if (backToTitleButton != null)
            backToTitleButton.gameObject.SetActive(isTournament);

        if (continueToNextRaceButton != null)
            continueToNextRaceButton.gameObject.SetActive(isTournament);

                // FULL
        if (backToFullTournamentMenuButton != null)
            backToFullTournamentMenuButton.gameObject.SetActive(isFull);

        if (backToTitleFullButton != null)
            backToTitleFullButton.gameObject.SetActive(isFull);

        if (continueFullTournamentButton != null)
            continueFullTournamentButton.gameObject.SetActive(isFull);
    }

    public void Open(List<RaceResultData> results)
    {
        currentResults = results;
        rootPanel.SetActive(true);

        var mode = GameManagerInstance.Instance?.currentGameMode;

        if (mode == GameMode.MiniTournament)
        {
            MiniTournamentManager.Instance?.ProcessRaceResults(results);
            UpdateContinueButtonStateMini();
        }
        else if (mode == GameMode.Tournament || mode == GameMode.StoryMode)
        {
            FullTournamentManager.Instance?.ProcessRaceResults(results);
            UpdateContinueButtonStateFull();
        }

        StringBuilder sb = new StringBuilder();
       // sb.AppendLine("<b>RANKING</b>\n");

        foreach (var r in results)
        {
            string playerTag = r.isPlayer ? " <color=yellow>(PLAYER)</color>" : "";
            sb.AppendLine($"{r.position}º  -  {r.racerName}{playerTag}  -  {Math.Round(r.points)} pts");
        }

        rankText.text = sb.ToString();
    }

    private void UpdateContinueButtonStateMini()
    {
        if (continueToNextRaceButton == null) return;

        bool hasNext = MiniTournamentManager.Instance?.HasNextRace() ?? false;
        bool nextUnlocked = false;

        if (hasNext)
        {
            int nextIndex = (MiniTournamentManager.Instance.CurrentState?.currentRaceIndex ?? 0) + 1;
            nextUnlocked = MiniTournamentManager.Instance.IsRaceUnlocked(nextIndex);
        }

        continueToNextRaceButton.interactable = hasNext && nextUnlocked;
    }

    private void UpdateContinueButtonStateFull()
    {
        if (continueFullTournamentButton == null) return;

        bool hasNext = FullTournamentManager.Instance?.HasNextRace() ?? false;
        bool nextUnlocked = false;

        if (hasNext)
        {
            int nextIndex = (FullTournamentManager.Instance.CurrentState?.currentRaceIndex ?? 0) + 1;
            nextUnlocked = FullTournamentManager.Instance.IsRaceUnlocked(nextIndex);
        }

        continueFullTournamentButton.interactable = hasNext && nextUnlocked;
    }


    // ========== BOTÕES QUICK PLAY ==========

    public void OnTryAgain()
    {
        RankingManager.Instance.TryAgain();
    }

    public void OnContinue()
    {
        RankingManager.Instance.Continue();
    }

    public void OnTryAnotherCircuit()
    {
        RankingManager.Instance.TryAnotherCircuit();
    }

    // ========== BOTÕES MINI TOURNAMENT ==========

    public void OnContinueToNextRace()
    {
        RankingManager.Instance.ContinueToNextRace();
    }

    public void OnBackToMiniTournamentMenu()
    {
        RankingManager.Instance.BackToTournamentMenu();
    }

    public void OnBackToTitle()
    {
        RankingManager.Instance.GiveUp();
    }

    // ========== BOTÕES FULL TOURNAMENT ==========

    // ========== BOTÕES FULL TOURNAMENT ==========

    public void OnContinueFullTournament()
    {
        RankingManager.Instance.ContinueFullTournament();
    }

    public void OnBackToFullTournamentMenu()
    {
        RankingManager.Instance.BackToFullTournamentMenu();
    }

    public void OnBackToTitleFull()
    {
        RankingManager.Instance.SaveAndQuitFullTournament();
}

}