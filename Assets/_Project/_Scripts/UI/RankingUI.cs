using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Text;

public class RankingUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject rootPanel;
    [SerializeField] private TextMeshProUGUI rankText;
    [SerializeField] private Button tryAgainButton;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button tryAnotherCircuitButton;

    void Awake()
    {
        bool isQuickRace = QuickPlayManagement.Instance != null;

        if (tryAnotherCircuitButton != null)
            tryAnotherCircuitButton.gameObject.SetActive(isQuickRace);
    }

    void Start()
    {
        rootPanel.SetActive(false);
    }

    public void Open(List<RaceResultData> results)
    {
        rootPanel.SetActive(true);

        System.Text.StringBuilder sb = new System.Text.StringBuilder();

        sb.AppendLine("<b>RANKING</b>\n");

        foreach (var r in results)
        {
            string playerTag = r.isPlayer ? " <color=yellow>(PLAYER)</color>" : "";

            sb.AppendLine(
                $"{r.position}º  -  {r.racerName}{playerTag}  -  {r.points} pts"
            );
        }

        rankText.text = sb.ToString();
    }


    public void OnTryAgain()
    {
        RankingManager.Instance.TryAgain();
    }

    public void OnBackToTitle()
    {
        RankingManager.Instance.Continue();
    }

    public void OnTryAnotherCircuit()
    {
        RankingManager.Instance.TryAnotherCircuit();
    }
}
