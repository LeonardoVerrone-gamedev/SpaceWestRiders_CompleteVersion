using System.Collections;
using UnityEngine;
using TMPro;

public class CarSelectionManagerScript : MonoBehaviour
{
    public static CarSelectionManagerScript Instance { get; private set; }

    [Header("HUD Panels")]
    [SerializeField] private GameObject SelectionMenuHUD_Singleplayer;
    [SerializeField] private GameObject SelectionMenuHUD_P1;
    [SerializeField] private GameObject SelectionMenuHUD_P2;

    [Header("Singleplayer Animators")]
    [SerializeField] private Animator leftArrowSingle;
    [SerializeField] private Animator rightArrowSingle;

    [Header("P1 Animators")]
    [SerializeField] private Animator leftArrowP1;
    [SerializeField] private Animator rightArrowP1;

    [Header("P2 Animators")]
    [SerializeField] private Animator leftArrowP2;
    [SerializeField] private Animator rightArrowP2;

    [Header("UI Text Elements")]
    [SerializeField] private TextMeshProUGUI contedownText;

    private bool p1AlreadySelectedACar;

    private Coroutine countdownCoroutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        contedownText.gameObject.SetActive(true);
        contedownText.text = "Jogador 1: Aperte qualquer botão para entrar!";
    }

    public void OnSideSelectionPressed(bool isSingleplayer, bool isP1, bool isRight)
    {
        if (isSingleplayer)
        {
            TriggerArrow(isRight ? rightArrowSingle : leftArrowSingle);
        }
        else if (isP1)
        {
            TriggerArrow(isRight ? rightArrowP1 : leftArrowP1);
        }
        else
        {
            TriggerArrow(isRight ? rightArrowP2 : leftArrowP2);
        }
    }

    private void TriggerArrow(Animator arrowAnimator)
    {
        if (arrowAnimator != null)
        {
            arrowAnimator.SetTrigger("Pulse");
        }
    }

    public void Show3SecondsCountdown()
    {
        if (countdownCoroutine != null)
        {
            StopCoroutine(countdownCoroutine);
        }

        countdownCoroutine = StartCoroutine(CountdownRoutine());
    }

    public void ShowP2JoinInstruction()
    {
        if (contedownText == null)
            return;

        contedownText.gameObject.SetActive(true);
        contedownText.text = "Jogador 2: Aperte qualquer botão para entrar";
    }

    private IEnumerator CountdownRoutine()
    {
        if (contedownText == null)
            yield break;

        contedownText.gameObject.SetActive(true);

        for (int i = 3; i > 0; i--)
        {
            contedownText.text =
                $"Jogador 2: Aperte qualquer botão para entrar ({i})...";

            yield return new WaitForSeconds(1f);
        }

        contedownText.text = "Jogador 2 Conectado!";

        countdownCoroutine = null;
    }

    /// <summary>
    /// Mostra a tela de seleção.
    /// isP2 = false -> P1 selecionando
    /// isP2 = true  -> P2 selecionando
    /// </summary>
    public void ShowSelectionHud(bool active, bool isP2)
    {
        if (!active)
        {
            SelectionMenuHUD_Singleplayer.SetActive(false);
            SelectionMenuHUD_P1.SetActive(false);
            SelectionMenuHUD_P2.SetActive(false);

            contedownText.gameObject.SetActive(false);

            return;
        }

        contedownText.gameObject.SetActive(false);

        if (!isP2)
        {
            // P1 precisa selecionar
            SelectionMenuHUD_Singleplayer.SetActive(true);
            SelectionMenuHUD_P1.SetActive(false);
            SelectionMenuHUD_P2.SetActive(false);
        }
        else
        {
            // P2 entrou
            SelectionMenuHUD_P2.SetActive(true);

            // Só mostra a HUD do P1 se ele AINDA não confirmou
            SelectionMenuHUD_P1.SetActive(!p1AlreadySelectedACar);

            SelectionMenuHUD_Singleplayer.SetActive(false);
        }
    }

    public void HidePlayerSelectionHud(bool isP2)
    {
        if (isP2)
        {
            SelectionMenuHUD_P2.SetActive(false);
        }
        else
        {
            SelectionMenuHUD_Singleplayer.SetActive(false);
            SelectionMenuHUD_P1.SetActive(false);
            
            p1AlreadySelectedACar = true;
        }
    }
}