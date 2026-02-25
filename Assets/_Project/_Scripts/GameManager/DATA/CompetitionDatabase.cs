using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class CompetitionDatabase : MonoBehaviour
{
    public static CompetitionDatabase Instance;

    [Header("All Competitions In Game")]
    [SerializeField] private List<CompetitionSO> competitions;

    private Dictionary<string, CompetitionSO> competitionLookup;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            BuildDictionary();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void BuildDictionary()
    {
        competitionLookup = new Dictionary<string, CompetitionSO>();

        foreach (var comp in competitions)
        {
            if (comp == null) continue;

            if (string.IsNullOrEmpty(comp.competitionID))
            {
                Debug.LogError($"Competition sem ID: {comp.name}");
                continue;
            }

            if (competitionLookup.ContainsKey(comp.competitionID))
            {
                Debug.LogError($"ID duplicado: {comp.competitionID}");
                continue;
            }

            competitionLookup.Add(comp.competitionID, comp);
        }
    }

    public CompetitionSO GetCompetitionByID(string id)
    {
        if (string.IsNullOrEmpty(id))
            return null;

        if (competitionLookup.TryGetValue(id, out var comp))
            return comp;

        Debug.LogError($"Competition ID não encontrado: {id}");
        return null;
    }
}