using UnityEngine;
using UnityEngine.Rendering.Universal; // Necessário para o DecalProjector

public class SCR_CarDecalsManager : MonoBehaviour
{
    [Header("Decal Materials")]
    [SerializeField] private Material[] lateralDecalOptions;
    [SerializeField] private Material[] frontalDecalOptions;
    [SerializeField] private Material[] topDecalOptions;
    [SerializeField] private Material[] backDecalOptions;

    [Header("Projector References")]
    [SerializeField] private DecalProjector[] lateralDecalProjector;
    [SerializeField] private DecalProjector frontalDecalProjector;
    [SerializeField] private DecalProjector topDecalProjector;
    [SerializeField] private DecalProjector backDecalProjector;

    public int currentDecalIndex;

    public bool inDecalSelection = false;

    void OnEnable()
    {
        RaceManager.OnDecalSelectionStart += StartSelection;
    }

    void OnDisable()
    {
        RaceManager.OnDecalSelectionStart -= StartSelection;
    }

    public void EndSelection()
    {
        if(!inDecalSelection) return;
        
        inDecalSelection = false;

        Debug.Log($"{transform.gameObject} finalizou seleção de decal");

        RaceManager.Instance.EndSelectionAndGoToStart();
        this.enabled = false;
    }

    void StartSelection()
    {
        if(GetComponent<RacerStatus>().isPlayer== false)return;
       inDecalSelection = true; 
       Debug.Log($"{transform.gameObject} na seleção de decal");
    }

    public void SetDecal(int input) // Input esperado: -1 ou 1
    {
        if(!inDecalSelection)return;
        // 1. Atualiza o índice
        currentDecalIndex += input;

        // 2. Lógica de "Loop" (se chegar no fim, volta ao início e vice-versa)
        // Usamos o lateralDecalOptions como base para o tamanho da lista
        if (currentDecalIndex < 0) 
            currentDecalIndex = lateralDecalOptions.Length - 1;
        else if (currentDecalIndex >= lateralDecalOptions.Length) 
            currentDecalIndex = 0;

        // 3. Aplica os materiais em todos os projetores
        UpdateProjector(frontalDecalProjector, frontalDecalOptions);
        UpdateProjector(lateralDecalProjector[0], lateralDecalOptions);
        UpdateProjector(lateralDecalProjector[1], lateralDecalOptions);
        UpdateProjector(topDecalProjector, topDecalOptions);
        UpdateProjector(backDecalProjector, backDecalOptions);
    }

    private void UpdateProjector(DecalProjector projector, Material[] options)
    {
        if (projector == null || options == null || options.Length == 0) return;

        // Verifica se existe um material válido no índice atual
        if (options[currentDecalIndex] != null)
        {
            projector.gameObject.SetActive(true);
            projector.material = options[currentDecalIndex];
        }
        else
        {
            // Se o slot estiver vazio, desativa o projetor para economizar performance
            projector.gameObject.SetActive(false);
        }
    }
}