
using UnityEngine;
using System.Collections.Generic;

public class GhostPrefabDatabase : MonoBehaviour
{
    public static GhostPrefabDatabase Instance { get; private set; }

    [Header("Ghost Car Prefabs")]
    [SerializeField] private List<GameObject> ghostPrefabs = new List<GameObject>();

    [Header("Spawn Settings")]
    [SerializeField] private Transform ghostContainer;

    private Dictionary<string, GameObject> prefabDatabase =
        new Dictionary<string, GameObject>();

    private readonly List<GameObject> spawnedGhosts =
        new List<GameObject>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        BuildDatabase();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // =====================================================
    // CONSTRUIR BANCO DE PREFABS
    // =====================================================

    public void BuildDatabase()
    {
        prefabDatabase.Clear();

        foreach (GameObject prefab in ghostPrefabs)
        {
            if (prefab == null)
                continue;

            SCR_CarIdentity identity =
                prefab.GetComponent<SCR_CarIdentity>();

            if (identity == null || identity.racerData == null)
            {
                Debug.LogWarning(
                    $"[GHOST DATABASE] Prefab {prefab.name} " +
                    "não possui SCR_CarIdentity ou racerData."
                );

                continue;
            }

            string characterID = identity.racerData.characterID;

            if (string.IsNullOrWhiteSpace(characterID))
            {
                Debug.LogWarning(
                    $"[GHOST DATABASE] Character ID inválido em {prefab.name}."
                );

                continue;
            }

            if (prefabDatabase.ContainsKey(characterID))
            {
                Debug.LogWarning(
                    $"[GHOST DATABASE] Character ID duplicado: {characterID}"
                );

                continue;
            }

            prefabDatabase.Add(characterID, prefab);

            Debug.Log(
                $"[GHOST DATABASE] Registrado: " +
                $"{prefab.name} | ID: {characterID}"
            );
        }

        Debug.Log(
            $"[GHOST DATABASE] {prefabDatabase.Count} prefabs registrados."
        );
    }

    // =====================================================
    // INSTANCIAR E REPRODUZIR GHOST
    // =====================================================

    public GameObject SpawnAndPlay(GhostRecordingData recording)
    {
        if (recording == null ||
            recording.frames == null ||
            recording.frames.Count < 2)
        {
            Debug.LogWarning(
                "[GHOST DATABASE] Gravação inválida."
            );

            return null;
        }

        if (string.IsNullOrWhiteSpace(recording.characterID))
        {
            Debug.LogWarning(
                "[GHOST DATABASE] Character ID ausente na gravação."
            );

            return null;
        }

        if (!prefabDatabase.TryGetValue(
            recording.characterID,
            out GameObject prefab))
        {
            Debug.LogWarning(
                $"[GHOST DATABASE] Nenhum prefab encontrado para " +
                $"Character ID: {recording.characterID}"
            );

            return null;
        }

        GhostFrameData firstFrame = recording.frames[0];

        GameObject ghost = Instantiate(
            prefab,
            firstFrame.position,
            firstFrame.rotation,
            ghostContainer
        );

        GhostPlayback playback =
            ghost.GetComponent<GhostPlayback>();

        if (playback == null)
        {
            Debug.LogError(
                $"[GHOST DATABASE] {prefab.name} não possui GhostPlayback."
            );

            Destroy(ghost);
            return null;
        }

        spawnedGhosts.Add(ghost);

        playback.Play(recording);

        Debug.Log(
            $"[GHOST DATABASE] Ghost instanciado!\n" +
            $"Carro: {recording.characterID}\n" +
            $"Frames: {recording.frames.Count}\n" +
            $"Tempo: {recording.totalTime:F3}s"
        );

        return ghost;
    }

    // =====================================================
    // LIMPAR GHOSTS INSTANCIADOS
    // =====================================================

    public void ClearSpawnedGhosts()
    {
        foreach (GameObject ghost in spawnedGhosts)
        {
            if (ghost != null)
                Destroy(ghost);
        }

        spawnedGhosts.Clear();
    }
}
