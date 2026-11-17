
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections;
using System;

public class UsernameRegistrationUI : MonoBehaviour
{
    [Header("Interface")]
    [SerializeField] private GameObject usernamePanel;
    [SerializeField] private TMP_InputField usernameInput;
    [SerializeField] private Button confirmButton;
    [SerializeField] private TMP_Text errorText;

    [Header("Settings")]
    [SerializeField] private int maxUsernameLength = 16;

    private const string UsernamePrefsKey = "PlayerUsername";
    private const string UsernameSyncedPrefsKey = "PlayerUsernameSynced";

    private void Awake()
    {
        confirmButton.onClick.AddListener(ConfirmUsername);

        if (errorText != null)
            errorText.text = "";
    }

    private void Start()
    {
        string savedUsername =
            PlayerPrefs.GetString(UsernamePrefsKey, "").Trim();

        if (string.IsNullOrWhiteSpace(savedUsername))
        {
            usernamePanel.SetActive(true);
        }
        else
        {
            usernamePanel.SetActive(false);

            // Tenta sincronizar registros antigos,
            // caso uma tentativa anterior tenha falhado.
            StartCoroutine(WaitAndSyncUsername());
        }
    }

    private void ConfirmUsername()
    {
        string username = usernameInput.text.Trim();

        if (string.IsNullOrWhiteSpace(username))
        {
            ShowError("Please enter a username.");
            return;
        }

        if (username.Length > maxUsernameLength)
        {
            ShowError(
                $"Maximum {maxUsernameLength} characters."
            );
            return;
        }

        // Salva localmente.
        PlayerPrefs.SetString(UsernamePrefsKey, username);

        // Exige sincronização com o novo username.
        PlayerPrefs.SetInt(UsernameSyncedPrefsKey, 0);

        PlayerPrefs.Save();

        Debug.Log(
            $"[USERNAME] Username salvo: {username}"
        );

        // Fecha a janela imediatamente.
        usernamePanel.SetActive(false);

        // Atualiza os registros antigos no Supabase.
        StartCoroutine(WaitAndSyncUsername());
    }

    private void ShowError(string message)
    {
        if (errorText != null)
            errorText.text = message;
    }

    private IEnumerator WaitAndSyncUsername()
    {
        // Aguarda o serviço e o PlayerId estarem disponíveis.
        while (
            SupabaseGhostService.Instance == null ||
            GameManagerInstance.Instance == null ||
            !Guid.TryParse(
                GameManagerInstance.Instance.PlayerId,
                out _
            )
        )
        {
            yield return null;
        }

        SupabaseGhostService.Instance.SyncSavedUsername();
    }

    private void OnDestroy()
    {
        if (confirmButton != null)
            confirmButton.onClick.RemoveListener(ConfirmUsername);
    }
}
