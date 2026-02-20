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
    }

    public void Open(List<RaceResultData> results)
    {
        currentResults = results;
        rootPanel.SetActive(true);

        // Se for torneio, processa os resultados antes de mostrar
        if (GameManagerInstance.Instance?.currentGameMode == GameMode.MiniTournament)
        {
            MiniTournamentManager.Instance?.ProcessRaceResults(results);
            UpdateContinueButtonState();
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<b>RANKING</b>\n");

        foreach (var r in results)
        {
            string playerTag = r.isPlayer ? " <color=yellow>(PLAYER)</color>" : "";
            sb.AppendLine($"{r.position}º  -  {r.racerName}{playerTag}  -  {Math.Round(r.points)} pts");
        }

        rankText.text = sb.ToString();
    }

    private void UpdateContinueButtonState()
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
}