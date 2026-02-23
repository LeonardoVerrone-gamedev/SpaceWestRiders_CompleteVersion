using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class FullTournamentMenu : MonoBehaviour
{
    [Header("Circuit Buttons")]
    [SerializeField] private Button[] circuitButtons;
    [SerializeField] private TextMeshProUGUI[] circuitNames;
    [SerializeField] private TextMeshProUGUI[] circuitStatus;

    [Header("Elimination Display")]
    [SerializeField] private TextMeshProUGUI eliminatedTeamsText;

    [Header("Navigation")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button giveUpButton;

    private void Start()
    {
        if (GameManagerInstance.Instance.currentGameMode != GameMode.Tournament)
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
        var state = FullTournamentManager.Instance?.CurrentState;
        if (state == null) return;

        var competition = state.competition;

        for (int i = 0; i < competition.circuits.Count; i++)
        {
            circuitNames[i].text = competition.circuits[i].circuitName;

            bool unlocked = FullTournamentManager.Instance.IsRaceUnlocked(i);
            bool completed = state.raceResults.ContainsKey(i);
            bool current = (i == state.currentRaceIndex);

            circuitButtons[i].interactable = unlocked;

            if (completed)
                circuitStatus[i].text = "COMPLETED";
            else if (current)
                circuitStatus[i].text = "CURRENT";
            else if (!unlocked)
                circuitStatus[i].text = "LOCKED";
            else
                circuitStatus[i].text = "READY";

            int index = i;
            circuitButtons[i].onClick.RemoveAllListeners();
            circuitButtons[i].onClick.AddListener(() =>
                FullTournamentManager.Instance.LoadRace(index));
        }
    }

    private void UpdateEliminatedTeamsDisplay()
    {
        var state = FullTournamentManager.Instance?.CurrentState;
        if (state == null) return;

        var allEliminations = state.eliminationHistory
            .eliminationsByRace
            .SelectMany(e => e.Value)
            .Distinct()
            .ToList();

        if (allEliminations.Count == 0)
        {
            eliminatedTeamsText.text = "Eliminated Teams: None";
            return;
        }

        string text = "Eliminated Teams:\n";
        foreach (var team in allEliminations)
            text += $"• {team.teamName}\n";

        eliminatedTeamsText.text = text;
    }

    private void UpdateContinueButton()
    {
        bool hasNext = FullTournamentManager.Instance.HasNextRace();
        bool nextUnlocked = false;

        if (hasNext)
        {
            int next = FullTournamentManager.Instance.CurrentState.currentRaceIndex + 1;
            nextUnlocked = FullTournamentManager.Instance.IsRaceUnlocked(next);
        }

        continueButton.interactable = hasNext && nextUnlocked;
    }

    public void OnContinue()
    {
        FullTournamentManager.Instance.AdvanceToNextRace();
    }

    public void OnGiveUp()
    {
        FullTournamentManager.Instance.SaveAndQuit();
    }

    public void OnKickP2()
    {
        FullTournamentManager.Instance.KickP2();
    }
}
