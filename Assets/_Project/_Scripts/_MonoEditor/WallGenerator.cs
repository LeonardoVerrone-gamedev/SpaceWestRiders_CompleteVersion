using System.Collections.Generic;
using UnityEngine;

public class WallGenerator : MonoBehaviour
{
    [Header("Configurações dos Pontos")]
    [Tooltip("Lista ordenada de pontos/transforms representando a linha/spline")]
    public List<Transform> points = new List<Transform>();

    [Header("Prefabs (Variedade)")]
    [Tooltip("Lista de prefabs de muralhas. O script sorteará um aleatório para cada ponto.")]
    public List<GameObject> wallSegmentPrefabs = new List<GameObject>();

    [Header("Organização")]
    [Tooltip("Transform pai para manter a hierarquia limpa")]
    public Transform container;

    [ContextMenu("Generate Wall")]
    public void Generate()
    {
        // Validação da lista de prefabs
        if (wallSegmentPrefabs == null || wallSegmentPrefabs.Count == 0)
        {
            Debug.LogError("Por favor, adicione pelo menos um prefab na lista 'Wall Segment Prefabs'.");
            return;
        }

        // Filtra nulos da lista de prefabs para evitar erros no sorteio
        List<GameObject> validPrefabs = wallSegmentPrefabs.FindAll(p => p != null);
        if (validPrefabs.Count == 0)
        {
            Debug.LogError("Todos os prefabs atribuídos na lista estão nulos/vazios!");
            return;
        }

        if (points == null || points.Count < 2)
        {
            Debug.LogError("Você precisa de pelo menos 2 pontos para gerar a muralha.");
            return;
        }

        ClearExistingWall();

        if (container == null)
        {
            GameObject containerGO = new GameObject("Generated_Wall_Container");
            containerGO.transform.SetParent(this.transform);
            container = containerGO.transform;
        }

        for (int i = 0; i < points.Count; i++)
        {
            if (points[i] == null) continue;

            Vector3 currentPos = points[i].position;
            Quaternion spawnRotation = Quaternion.identity;

            Vector3 direction = Vector3.zero;

            // Determina a direção do segmento atual
            if (i < points.Count - 1 && points[i + 1] != null)
            {
                direction = points[i + 1].position - currentPos;
            }
            else if (i > 0 && points[i - 1] != null)
            {
                direction = currentPos - points[i - 1].position;
            }

            // Garante que o cálculo ignore inclinações verticais (X e Z de rotação serão 0)
            direction.y = 0;

            if (direction.sqrMagnitude > 0.001f)
            {
                // Alinha o eixo X (seta vermelha) do prefab com a direção da linha
                float angleY = Mathf.Atan2(direction.z, direction.x) * Mathf.Rad2Deg;

                // Aplica a rotação apenas no eixo Y
                spawnRotation = Quaternion.Euler(0f, -angleY, 0f);
            }

            // Sorteia um prefab aleatório da lista
            int randomIndex = Random.Range(0, validPrefabs.Count);
            GameObject selectedPrefab = validPrefabs[randomIndex];

            // Instancia o prefab sorteado no ponto atual
            GameObject newSegment = Instantiate(selectedPrefab, currentPos, spawnRotation, container);
            newSegment.name = $"WallSegment_{i}_{selectedPrefab.name}";
        }

        Debug.Log($"Muralha gerada com sucesso ({points.Count} segmentos com variação de prefabs)!");
    }

    [ContextMenu("Clear Wall")]
    public void ClearExistingWall()
    {
        if (container == null) return;

        for (int i = container.childCount - 1; i >= 0; i--)
        {
            DestroyImmediate(container.GetChild(i).gameObject);
        }
    }
}