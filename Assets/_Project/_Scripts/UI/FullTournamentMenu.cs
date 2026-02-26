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
    [Header("Race Results Display")]
    [SerializeField] private TextMeshProUGUI[] raceResultsTexts;

    [Header("Elimination Display")]
    [SerializeField] private TextMeshProUGUI eliminatedTeamsText;

    [Header("Global Ranking Display")]
    [SerializeField] private TextMeshProUGUI teamRankingText;
    [SerializeField] private TextMeshProUGUI driverRankingText;

    [Header("Navigation")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button giveUpButton;

    private void Start()
    {
        if (GameManagerInstance.Instance.currentGameMode != GameMode.Tournament && GameManagerInstance.Instance.currentGameMode != GameMode.StoryMode)
        {
            gameObject.SetActive(false);
            return;
        }

        UpdateCircuitButtons();
        UpdateEliminatedTeamsDisplay();
        UpdateContinueButton();
        UpdateRaceResultsDisplay();
        UpdateTeamRankingDisplay();
        UpdateDriverRankingDisplay();
    }

    private void UpdateRaceResultsDisplay()
    {
        var state = FullTournamentManager.Instance?.CurrentState;

        if (state == null)
        {
            Debug.Log("NULL STATE");
            return;
        }

        if (raceResultsTexts == null)
        {
            Debug.Log("NULL race text");
            return;
        }

        for (int i = 0; i < raceResultsTexts.Length; i++)
        {
            if (i >= state.competition.circuits.Count)
            {
                raceResultsTexts[i].text = "";
                continue;
            }

            var results = state.GetRaceResults(i);

            if (results == null || results.Count == 0)
            {
                raceResultsTexts[i].text = "-";
                continue;
            }

            var podium = results
                .OrderBy(r => r.position)
                .Take(3)
                .ToList();

            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            foreach (var r in podium)
            {
                string medal = r.position switch
                {
                    1 => "1º",
                    2 => "2º",
                    3 => "3º",
                    _ => $"{r.position}º"
                };

                sb.AppendLine($"{medal} {r.racerName}");
            }

            raceResultsTexts[i].text = sb.ToString();
        }
    }

    private void UpdateTeamRankingDisplay()
    {
        if (teamRankingText == null) return;

        var ranking = FullTournamentManager.Instance.GetTeamRanking();

        if (ranking == null || ranking.Count == 0)
        {
            teamRankingText.text = "No team standings yet.";
            return;
        }

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("TEAM STANDINGS\n");

        int position = 1;

        foreach (var entry in ranking)
        {
            sb.AppendLine($"{position}º  {entry.team.teamName}  -  {entry.points} pts");
            position++;
        }

        teamRankingText.text = sb.ToString();
    }

    private void UpdateDriverRankingDisplay()
    {
        if (driverRankingText == null) return;

        var ranking = FullTournamentManager.Instance.GetDriverRanking();

        if (ranking == null || ranking.Count == 0)
        {
            driverRankingText.text = "No driver standings yet.";
            return;
        }

        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        sb.AppendLine("DRIVER STANDINGS\n");

        int position = 1;

        foreach (var entry in ranking)
        {
            sb.AppendLine($"{position}º  {entry.racerName}  -  {entry.points} pts");
            position++;
        }

        driverRankingText.text = sb.ToString();
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

        //continueButton.interactable = hasNext && nextUnlocked;
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
