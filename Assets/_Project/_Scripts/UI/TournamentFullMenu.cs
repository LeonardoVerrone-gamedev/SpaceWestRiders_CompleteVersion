using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class TournamentFullMenu : MonoBehaviour
{
    [Header("Circuit Buttons")]
    [SerializeField] private Button[] circuitButtons; // 20 botões, um para cada circuito
    [SerializeField] private TextMeshProUGUI[] circuitNames; // Nomes dos circuitos
    [SerializeField] private TextMeshProUGUI[] circuitStatus; // Status (disputada/bloqueada)

    [Header("Elimination Display")]
    [SerializeField] private TextMeshProUGUI eliminatedTeamsText; // Mostra times eliminados
    [SerializeField] private TextMeshProUGUI eliminationWarningsText; // Mostra quando serão as eliminações

    [Header("Navigation")]
    [SerializeField] private Button continueButton; // Continuar para próxima corrida
    [SerializeField] private Button backButton; // Voltar ao menu do torneio
    [SerializeField] private Button giveUpButton; // Desistir (voltar ao title)

    [Header("References")]
    [SerializeField] private RankingManager rankingManager;

    [Header("Elimination Races")]
    [SerializeField] private int[] eliminationRaces; // 8ª, 16ª e 19ª (0-based)

    private void Start()
    {
        // Verifica se estamos no modo Tournament
        if (GameManagerInstance.Instance.currentGameMode != GameMode.Tournament)
        {
            gameObject.SetActive(false);
            return;
        }

        eliminationRaces = TournamentFullManager.Instance.CurrentState.competition.EliminateAt;

        UpdateCircuitButtons();
        UpdateEliminatedTeamsDisplay();
        UpdateEliminationWarnings();
        UpdateContinueButton();
    }

    private void UpdateCircuitButtons()
    {
        var state = TournamentFullManager.Instance?.CurrentState;
        if (state == null) return;

        var competition = state.competition;
        if (competition == null || competition.circuits.Count < circuitButtons.Length) return;

        // Atualiza nomes dos circuitos
        for (int i = 0; i < circuitButtons.Length && i < competition.circuits.Count; i++)
        {
            if (circuitNames[i] != null)
                circuitNames[i].text = competition.circuits[i].circuitName;
        }

        // Atualiza status e interatividade dos botões
        for (int i = 0; i < circuitButtons.Length; i++)
        {
            bool isUnlocked = IsRaceUnlocked(i);
            bool isCurrentRace = (i == state.currentRaceIndex);
            bool isCompleted = state.raceResults.ContainsKey(i);
            bool isEliminationRace = eliminationRaces.Contains(i);

            circuitButtons[i].interactable = isUnlocked;

            if (circuitStatus[i] != null)
            {
                string statusText = "";
                
                if (isCompleted)
                    statusText = "COMPLETED";
                else if (isCurrentRace)
                    statusText = "CURRENT";
                else if (!isUnlocked)
                    statusText = "LOCKED";
                else if (isEliminationRace)
                    statusText = "ELIMINATION RACE";
                else
                    statusText = "READY";
                    
                circuitStatus[i].text = statusText;
            }

            // Remove listeners antigos e adiciona novo
            circuitButtons[i].onClick.RemoveAllListeners();
            int raceIndex = i; // Captura para o lambda
            circuitButtons[i].onClick.AddListener(() => LoadRace(raceIndex));
        }
    }

    private bool IsRaceUnlocked(int raceIndex)
    {
        var state = TournamentFullManager.Instance?.CurrentState;
        if (state == null) return false;

        // Primeira corrida sempre liberada
        if (raceIndex == 0) return true;

        // Precisa ter disputado a anterior
        bool previousPlayed = state.raceResults.ContainsKey(raceIndex - 1);
        if (!previousPlayed) return false;

        // Precisa ter pelo menos um jogador vivo
        bool anyPlayerAlive = SCR_PersistentData.Instance.players
            .Any(p => !state.eliminatedTeams.Contains(p.selectedCarData.team));

        return anyPlayerAlive;
    }

    private void UpdateEliminatedTeamsDisplay()
    {
        if (eliminatedTeamsText == null) return;

        var state = TournamentFullManager.Instance?.CurrentState;
        if (state == null || state.eliminatedTeams.Count == 0)
        {
            eliminatedTeamsText.text = "Eliminated Teams: None";
            return;
        }

        string eliminated = "<b>Eliminated Teams:</b>\n";
        foreach (var team in state.eliminatedTeams)
        {
            eliminated += $"• {team.teamName}\n";
        }
        eliminatedTeamsText.text = eliminated;
    }

    private void UpdateEliminationWarnings()
    {
        if (eliminationWarningsText == null) return;

        var state = TournamentFullManager.Instance?.CurrentState;
        if (state == null) return;

        int racesCompleted = state.raceResults.Count;
        List<string> upcomingEliminations = new List<string>();

        foreach (int raceIndex in eliminationRaces)
        {
            if (racesCompleted <= raceIndex)
            {
                int raceNumber = raceIndex + 1;
                upcomingEliminations.Add($"Race {raceNumber}");
            }
        }

        if (upcomingEliminations.Count > 0)
        {
            eliminationWarningsText.text = $"<color=yellow>Eliminations after: {string.Join(", ", upcomingEliminations)}</color>";
        }
        else
        {
            eliminationWarningsText.text = "";
        }
    }

    private void UpdateContinueButton()
    {
        if (continueButton == null) return;

        bool hasNext = TournamentFullManager.Instance?.HasNextRace() ?? false;
        bool nextUnlocked = false;

        if (hasNext)
        {
            int nextIndex = (TournamentFullManager.Instance.CurrentState?.currentRaceIndex ?? 0) + 1;
            nextUnlocked = IsRaceUnlocked(nextIndex);
        }

        continueButton.interactable = hasNext && nextUnlocked;
    }

    private void LoadRace(int raceIndex)
    {
        TournamentFullManager.Instance?.LoadRace(raceIndex);
    }

    // ========== BOTÕES DE NAVEGAÇÃO ==========

    public void OnContinueToNextRace()
    {
        TournamentFullManager.Instance?.AdvanceToNextRace();
    }

    public void OnBackToMenu()
    {
        // Volta para o menu principal do torneio
        SceneManager.LoadScene("TournamentFullMainMenu");
    }

    public void OnGiveUp()
    {
        // Desiste do torneio e volta ao title
        TournamentFullManager.Instance?.EndTournament();
    }
}