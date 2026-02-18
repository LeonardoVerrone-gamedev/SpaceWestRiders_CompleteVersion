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
        if(Instance != null) Destroy(this);
        else Instance = this; DontDestroyOnLoad(this);
    }

    public void CreateCircuit(string circuit = null)
    {
        if (circuit == null || circuit == "")
        {
            List<CircuitSO> selected = new List<CircuitSO>
            {
                freeCircuits[Random.Range(0, freeCircuits.Length)]
            };

            competition = ScriptableObject.CreateInstance<CompetitionSO>();
            competition.Inicializar(selected, null, false, true, null);
        }

        else
        {
            selectedCircuit = freeCircuits[0];

            foreach(CircuitSO c in freeCircuits)
            {
                if(c.circuitID == circuit) selectedCircuit = c;
            }

            List<CircuitSO> selected = new List<CircuitSO>
            {
                selectedCircuit
            };

            competition = ScriptableObject.CreateInstance<CompetitionSO>();
            competition.Inicializar(selected, null, false, true, null);
        }

        StartRace();
    }

    public void StartRace()
    {
        SceneManager.LoadScene(competition.circuits[0].sceneName);

        //espera carregar
        //procura RaceManager e seta o numero de voltas para selectedCircuit.lapCount
    }
}