using UnityEngine;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

public class QuickPlayManagement : MonoBehaviour
{
    public static QuickPlayManagement Instance;

    public CompetitionSO competition;
    public CircuitSO[] freeCircuits;

    CircuitSO selectedCircuit = null;

    void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        GameManagerInstance.Instance.SetGameMode(GameMode.QuickPlay);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "TitleScreen")
        {
            Destroy(this);
        }
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    public void CreateCircuit(string circuit = null)
    {
        if (string.IsNullOrEmpty(circuit))
        {
            selectedCircuit = freeCircuits[Random.Range(0, freeCircuits.Length)];
        }
        else
        {
            selectedCircuit = freeCircuits[0];

            foreach (CircuitSO c in freeCircuits)
            {
                if (c.circuitID == circuit)
                {
                    selectedCircuit = c;
                    break;
                }
            }
        }

        List<CircuitSO> selected = new List<CircuitSO> { selectedCircuit };

        competition = ScriptableObject.CreateInstance<CompetitionSO>();
        competition.Inicializar(selected, null, false, true, null);

        StartRace();
    }

    public void StartRace()
    {
        SceneTransitionAnimationManager.Instance.LoadScene(selectedCircuit.sceneName);
    }//

    // ==========================
    // CHAMADOS PELO RANKING
    // ==========================

    public void ReturnToMenu()
    {
        KillPlayerData();
        SceneTransitionAnimationManager.Instance.LoadScene("TitleScreen");
    }

    public void TryAgain()
    {
        SceneTransitionAnimationManager.Instance.LoadScene(selectedCircuit.sceneName);
    }

    public void TryAnotherCircuit()
    {
        KillPlayerData();
        SceneTransitionAnimationManager.Instance.LoadScene("QuickRaceSetupScene");
    }

    void KillPlayerData()
    {
        SCR_PersistentData.Instance.FullReset();
    }
}
