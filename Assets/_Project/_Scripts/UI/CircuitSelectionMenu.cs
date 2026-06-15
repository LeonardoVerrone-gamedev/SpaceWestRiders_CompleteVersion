using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class CircuitSelection : MonoBehaviour
{
    [Header("Configurações de Circuitos")]
    public CircuitSO[] allFreeCircuits;
    [Tooltip("Lista de corredores disponíveis no jogo para sortear o Record Holder caso esteja NONE")]
    public List<RacerProfileSO> backupRacerProfiles;

    [Header("Layouts dos Planetas")]
    public GameObject Planet1Layout;
    public GameObject Planet2Layout;
    public GameObject Planet3Layout;
    public GameObject Planet4Layout;
    private List<GameObject> allLayouts = new List<GameObject>();

    [Header("Exibição do Trajeto (UI)")]
    [SerializeField] private Image displayCircuitoImage;

    [Header("Exibição de Dados Extras (UI)")]
    [SerializeField] private Slider dificuldadeSlider;
    [SerializeField] private TextMeshProUGUI recordTimeText;
    [SerializeField] private TextMeshProUGUI recordHolderText;
    [SerializeField] private TextMeshProUGUI trackName;

    private CircuitButtonData lastButton;
    public string lastButtonName;

    void Start()
    {
        allLayouts.Add(Planet1Layout);
        allLayouts.Add(Planet2Layout);
        allLayouts.Add(Planet3Layout);
        allLayouts.Add(Planet4Layout);
    }

    // Método centralizado acionado por Eventos (Garante funcionamento no Joystick e Mouse)
    public void NotificarNovoBotaoFocado(CircuitButtonData novoBotao)
    {
        if (novoBotao == null || novoBotao == lastButton)
            return;

        lastButton = novoBotao;
        lastButtonName = novoBotao.circuitID;

        Debug.Log($"Atualizando via Evento para {novoBotao.circuitID}");

        AtualizarDisplayComCircuito(novoBotao.circuitID);
    }

    public void RandomPlay()
    {
        if (QuickPlayManagement.Instance != null)
        {
            QuickPlayManagement.Instance.CreateCircuit(null);
        }
    }

    public void PlayCircuit(string circuitID)
    {
        if (QuickPlayManagement.Instance != null)
        {
            QuickPlayManagement.Instance.CreateCircuit(circuitID);
        }  
    }

    public void ShowOrHidePlanetLayout(GameObject layout)
    {
        bool estavaAtivo = layout.activeInHierarchy;

        foreach (GameObject _otherLayout in allLayouts)
        {
            if (_otherLayout != null)
            {
                _otherLayout.SetActive(false);
            }
        }

        layout.SetActive(!estavaAtivo);
    }

    public void AtualizarDisplayComCircuito(string circuitID)
    {
        if (allFreeCircuits == null || displayCircuitoImage == null) return;

        foreach (CircuitSO so in allFreeCircuits)
        {
            if (so != null && (so.name == circuitID || so.circuitID == circuitID))
            {
                // 1. Atualiza imagem do trajeto
                if (so.circuitImage != null)
                {
                    displayCircuitoImage.sprite = so.circuitImage;
                    displayCircuitoImage.enabled = true;
                }

                // 2. Atualiza o Slider de Dificuldade
                if (dificuldadeSlider != null)
                {
                    dificuldadeSlider.value = so.DifficultyLevel;
                }

                // 3. Atualiza os Recordes
                CarregarEExibirRecordes(so.circuitName);

                break;
            }
        }
    }

    private void CarregarEExibirRecordes(string circuitName)
    {
        string circuitTimeKey = "CIRCUIT_RECORD_TIME_" + circuitName;
        string circuitHolderKey = "CIRCUIT_RECORD_RACER_" + circuitName;

        if (trackName != null) trackName.text = circuitName;

        float recordTime = PlayerPrefs.GetFloat(circuitTimeKey, float.MaxValue);
        string recordHolder = PlayerPrefs.GetString(circuitHolderKey, "NONE");

        if (recordTimeText != null)
        {
            if (recordTime == float.MaxValue)
            {
                recordTimeText.text = "--:--.--_";
            }
            else
            {
                recordTimeText.text = FormatarTempo(recordTime);
            }
        }

        if (recordHolderText != null)
        {
            if (recordHolder == "NONE")
            {
                if (backupRacerProfiles != null && backupRacerProfiles.Count > 0)
                {
                    int randomIndex = Random.Range(0, backupRacerProfiles.Count);
                    string sorteado = backupRacerProfiles[randomIndex].racerName;
                    
                    PlayerPrefs.SetString(circuitHolderKey, sorteado);
                    PlayerPrefs.Save();

                    recordHolderText.text = sorteado;
                }
                else
                {
                    recordHolderText.text = "DESCONHECIDO";
                }
            }
            else
            {
                recordHolderText.text = recordHolder.ToUpper();
            }
        }
    }

    private string FormatarTempo(float tempoEmSegundos)
    {
        int minutos = Mathf.FloorToInt(tempoEmSegundos / 60f);
        int segundos = Mathf.FloorToInt(tempoEmSegundos % 60f);
        int milissegundos = Mathf.FloorToInt((tempoEmSegundos * 1000f) % 1000f);

        return string.Format("{0}:{1:00}.{2:000}", minutos, segundos, milissegundos);
    }

    void OnDisable()
    {
        lastButton = null;
        lastButtonName = string.Empty;
    }
}