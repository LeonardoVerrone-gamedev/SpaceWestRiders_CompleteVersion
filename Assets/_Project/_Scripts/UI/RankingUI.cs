using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Text;
using System;

public class RankingUI : MonoBehaviour
{
    public static RankingUI Instance { get; private set; }

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

    public bool OnRanking()
    {
        return rootPanel.activeInHierarchy;
    } 


    [Header("Button Labels")]
    [SerializeField] private TextMeshProUGUI continueToNextRaceLabel;

    private List<RaceResultData> currentResults;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // Configura botões baseado no modo de jogo
        ConfigureButtonsForGameMode();
    }

    void Start()
    {
        rootPanel.SetActive(false);
    }

    private void OnDestroy()
    {
        // Limpa a referência quando a cena for descarregada ou o objeto destruído
        if (Instance == this)
        {
            Instance = null;
        }
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

    [Header("Grid Layout Settings")]
    [SerializeField] private float posColumnX = 30f;      // Posição X da 1ª coluna (Posição: 1, 2, 3...)
    [SerializeField] private float nameColumnX = 140f;    // Posição X da 2ª coluna (Nome do piloto)
    [SerializeField] private float pointsColumnX = 380f;  // Posição X da 3ª coluna (Pontuação)
    [SerializeField] private float customLineHeight = 32f; // Altura em pixels de cada linha da tabela
    [SerializeField] private int totalGridRows = 8;        // Quantidade fixa de linhas da tabela UI

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

        // Aplica a altura fixa de linha para casar com a grade
        sb.Append($"<line-height={customLineHeight}px>");

        for (int i = 0; i < totalGridRows; i++)
        {
            if (i < results.Count)
            {
                var r = results[i];
                string playerTag = r.isPlayer ? " <color=yellow>(YOU)</color>" : "";
                string pointsStr = Math.Round(r.points).ToString("0000"); // Formata com zeros à esquerda se desejar

                // Posiciona cada elemento no eixo X exato da sua caixa na UI
                sb.AppendLine($"<pos={posColumnX}>{r.position}<pos={nameColumnX}>{r.racerName}{playerTag}<pos={pointsColumnX}>{pointsStr}");
            }
            else
            {
                // Linhas vazias para manter o alinhamento da grade
                sb.AppendLine($"<pos={posColumnX}>{i + 1}<pos={nameColumnX}>-<pos={pointsColumnX}>-");
            }
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

#if UNITY_EDITOR
[ContextMenu("Testar Layout no Inspector (Editor)")]
public void TestLayoutInEditor()
{
    // Dados mockados com 8 pilotos, posições e pontuações variadas
    List<RaceResultData> mockResults = new List<RaceResultData>
    {
        new RaceResultData { position = 1, racerName = "Danny Tongue", points = 12300, isPlayer = false },
        new RaceResultData { position = 2, racerName = "Viper",         points = 9500,  isPlayer = true  },
        new RaceResultData { position = 3, racerName = "J. Cross",      points = 8100,  isPlayer = false },
        new RaceResultData { position = 4, racerName = "Capitain Bee",  points = 6400,  isPlayer = false },
        new RaceResultData { position = 5, racerName = "T'wink",   points = 5000,  isPlayer = false },
        new RaceResultData { position = 6, racerName = "Viper",         points = 3200,  isPlayer = false },
        new RaceResultData { position = 7, racerName = "Billy Sin",         points = 1800,  isPlayer = false },
        new RaceResultData { position = 8, racerName = "Zäh",        points = 500,   isPlayer = false }
    };

    // Monta a string formatada usando as configurações de coluna e altura de linha
    System.Text.StringBuilder sb = new System.Text.StringBuilder();
    sb.Append($"<line-height={customLineHeight}px>");

    for (int i = 0; i < totalGridRows; i++)
    {
        if (i < mockResults.Count)
        {
            var r = mockResults[i];
            string playerTag = r.isPlayer ? " <color=yellow>(YOU)</color>" : "";
            string pointsStr = System.Math.Round(r.points).ToString("0000");

            sb.AppendLine($"<pos={posColumnX}>{r.position}<pos={nameColumnX}>{r.racerName}{playerTag}<pos={pointsColumnX}>{pointsStr}");
        }
    }

    if (rankText != null)
    {
        rankText.text = sb.ToString();
        // Força a atualização do canvas na Scene/Game View fora do Play Mode
        UnityEditor.EditorUtility.SetDirty(rankText);
    }
}

// Atualiza o texto automaticamente no Editor sempre que você alterar um valor no Inspector
private void OnValidate()
{
    if (!Application.isPlaying && rankText != null)
    {
        TestLayoutInEditor();
    }
}
#endif

}