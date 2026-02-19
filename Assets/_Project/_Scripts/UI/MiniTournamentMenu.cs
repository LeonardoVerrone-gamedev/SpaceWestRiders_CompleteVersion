using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class MiniTournamentMenu : MonoBehaviour
{
    [Header("Circuit Buttons")]
    [SerializeField] private Button[] circuitButtons; // 4 botões, um para cada circuito
    [SerializeField] private TextMeshProUGUI[] circuitNames; // Nomes dos circuitos
    [SerializeField] private TextMeshProUGUI[] circuitStatus; // Status (disputada/bloqueada)

    [Header("Elimination Display")]
    [SerializeField] private TextMeshProUGUI eliminatedTeamsText; // Mostra times eliminados

    [Header("Navigation")]
    [SerializeField] private Button continueButton; // Continuar para próxima corrida
    [SerializeField] private Button backButton; // Voltar ao menu do torneio
    [SerializeField] private Button giveUpButton; // Desistir (voltar ao title)

    [Header("References")]
    [SerializeField] private RankingManager rankingManager;

    private void Start()
    {
        // Verifica se estamos no modo MiniTournament
        if (GameManagerInstance.Instance.currentGameMode != GameMode.MiniTournament)
        {
            gameObject.SetActive(false);
            return;
        }

        UpdateCircuitButtons();
        UpdateEliminatedTeamsDisplay();
        UpdateContinueButton();
    }

    private void UpdateCircuitButtons()
    {
        var state = MiniTournamentManager.Instance?.CurrentState;
        if (state == null) return;

        var competition = state.competition;
        if (competition == null || competition.circuits.Count < 4) return;

        // Atualiza nomes dos circuitos
        for (int i = 0; i < circuitNames.Length && i < competition.circuits.Count; i++)
        {
            circuitNames[i].text = competition.circuits[i].circuitName;
        }

        // Atualiza status e interatividade dos botões
        for (int i = 0; i < circuitButtons.Length; i++)
        {
            bool isUnlocked = IsRaceUnlocked(i);
            bool isCurrentRace = (i == state.currentRaceIndex);
            bool isCompleted = state.raceResults.ContainsKey(i);

            circuitButtons[i].interactable = isUnlocked;

            if (circuitStatus[i] != null)
            {
                if (isCompleted)
                    circuitStatus[i].text = "COMPLETED";
                else if (isCurrentRace)
                    circuitStatus[i].text = "CURRENT";
                else if (!isUnlocked)
                    circuitStatus[i].text = "LOCKED";
                else
                    circuitStatus[i].text = "READY";
            }

            // Remove listeners antigos e adiciona novo
            circuitButtons[i].onClick.RemoveAllListeners();
            int raceIndex = i; // Captura para o lambda
            circuitButtons[i].onClick.AddListener(() => LoadRace(raceIndex));
        }
    }

    private bool IsRaceUnlocked(int raceIndex)
    {
        var state = MiniTournamentManager.Instance?.CurrentState;
        if (state == null) return false;

        // Corridas já disputadas estão SEMPRE liberadas (pode rejogar)
        if (state.raceResults.ContainsKey(raceIndex))
            return true;

        // Primeira corrida não disputada ainda
        if (raceIndex == 0) return true;

        // Para corridas futuras, precisa ter disputado a anterior
        bool previousPlayed = state.raceResults.ContainsKey(raceIndex - 1);
        if (!previousPlayed) return false;

        // Para corridas futuras, precisa ter pelo menos um jogador vivo
        bool anyPlayerAlive = SCR_PersistentData.Instance.players
            .Any(p => !state.eliminatedTeams.Contains(p.selectedCarData.team));

        return anyPlayerAlive;
    }

    private void UpdateEliminatedTeamsDisplay()
    {
        if (eliminatedTeamsText == null) return;

        var state = MiniTournamentManager.Instance?.CurrentState;
        if (state == null || state.eliminatedTeams.Count == 0)
        {
            eliminatedTeamsText.text = "Eliminated Teams: None";
            return;
        }

        string eliminated = "Eliminated Teams:\n";
        foreach (var team in state.eliminatedTeams)
        {
            eliminated += $"• {team.teamName}\n";
        }
        eliminatedTeamsText.text = eliminated;
    }

    private void UpdateContinueButton()
    {
        if (continueButton == null) return;

        bool hasNext = MiniTournamentManager.Instance?.HasNextRace() ?? false;
        bool nextUnlocked = false;

        if (hasNext)
        {
            int nextIndex = (MiniTournamentManager.Instance.CurrentState?.currentRaceIndex ?? 0) + 1;
            nextUnlocked = IsRaceUnlocked(nextIndex);
        }

        continueButton.interactable = hasNext && nextUnlocked;
    }

    private void LoadRace(int raceIndex)
    {
        MiniTournamentManager.Instance?.LoadRace(raceIndex);
    }

    // ========== BOTÕES DE NAVEGAÇÃO ==========

    public void OnContinueToNextRace()
    {
        MiniTournamentManager.Instance?.AdvanceToNextRace();
    }

    public void OnBackToMenu()
    {
        // Volta para o menu principal do torneio (cena de seleção)
        SceneManager.LoadScene("MiniTournamentMenu"); // Ajuste o nome da cena
    }

    public void OnGiveUp()
    {
        // Desiste do torneio e volta ao title
        MiniTournamentManager.Instance?.EndTournament();
    }
}