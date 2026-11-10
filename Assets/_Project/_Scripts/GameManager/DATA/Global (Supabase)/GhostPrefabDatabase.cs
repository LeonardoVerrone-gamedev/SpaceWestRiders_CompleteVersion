
using UnityEngine;
using System.Collections.Generic;

public class GhostPrefabDatabase : MonoBehaviour
{
    public static GhostPrefabDatabase Instance { get; private set; }

    [Header("Global Ghost Car Prefabs - Blue")]
    [SerializeField] private List<GameObject> ghostPrefabs = new List<GameObject>();

    [Header("Local Ghost Car Prefabs - Orange")]
    [SerializeField] private List<GameObject> localGhostPrefabs = new List<GameObject>();

    [Header("Spawn Settings")]
    [SerializeField] private Transform ghostContainer;

    private Dictionary<string, GameObject> prefabDatabase =
        new Dictionary<string, GameObject>();

    private Dictionary<string, GameObject> localPrefabDatabase =
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
    // CONSTRUIR OS DOIS BANCOS DE PREFABS
    // =====================================================

    public void BuildDatabase()
    {
        BuildDatabaseFromList(
            ghostPrefabs,
            prefabDatabase,
            "GLOBAL"
        );

        BuildDatabaseFromList(
            localGhostPrefabs,
            localPrefabDatabase,
            "LOCAL"
        );

        Debug.Log(
            $"[GHOST DATABASE] Banco global: {prefabDatabase.Count} | " +
            $"Banco local: {localPrefabDatabase.Count}"
        );
    }

    private void BuildDatabaseFromList(
        List<GameObject> prefabs,
        Dictionary<string, GameObject> database,
        string databaseType)
    {
        database.Clear();

        foreach (GameObject prefab in prefabs)
        {
            if (prefab == null)
                continue;

            SCR_CarIdentity identity =
                prefab.GetComponent<SCR_CarIdentity>();

            if (identity == null || identity.racerData == null)
            {
                Debug.LogWarning(
                    $"[GHOST DATABASE - {databaseType}] Prefab " +
                    $"{prefab.name} não possui SCR_CarIdentity ou racerData."
                );

                continue;
            }

            string characterID = identity.racerData.characterID;

            if (string.IsNullOrWhiteSpace(characterID))
            {
                Debug.LogWarning(
                    $"[GHOST DATABASE - {databaseType}] " +
                    $"Character ID inválido em {prefab.name}."
                );

                continue;
            }

            if (database.ContainsKey(characterID))
            {
                Debug.LogWarning(
                    $"[GHOST DATABASE - {databaseType}] " +
                    $"Character ID duplicado: {characterID}"
                );

                continue;
            }

            database.Add(characterID, prefab);

            Debug.Log(
                $"[GHOST DATABASE - {databaseType}] Registrado: " +
                $"{prefab.name} | ID: {characterID}"
            );
        }
    }

    // =====================================================
    // INSTANCIAR GHOST GLOBAL - AZUL
    // =====================================================

    public GameObject SpawnAndPlay(GhostRecordingData recording)
    {
        return SpawnAndPlayInternal(
            recording,
            prefabDatabase,
            "GLOBAL"
        );
    }

    // =====================================================
    // INSTANCIAR GHOST LOCAL - LARANJA
    // =====================================================

    public GameObject SpawnAndPlayLocal(GhostRecordingData recording)
    {
        return SpawnAndPlayInternal(
            recording,
            localPrefabDatabase,
            "LOCAL"
        );
    }

    // =====================================================
    // LÓGICA COMPARTILHADA DE SPAWN E PLAYBACK
    // =====================================================

    private GameObject SpawnAndPlayInternal(
        GhostRecordingData recording,
        Dictionary<string, GameObject> database,
        string ghostType)
    {
        if (recording == null ||
            recording.frames == null ||
            recording.frames.Count < 2)
        {
            Debug.LogWarning(
                $"[GHOST DATABASE - {ghostType}] Gravação inválida."
            );

            return null;
        }

        if (string.IsNullOrWhiteSpace(recording.characterID))
        {
            Debug.LogWarning(
                $"[GHOST DATABASE - {ghostType}] " +
                "Character ID ausente na gravação."
            );

            return null;
        }

        if (!database.TryGetValue(
            recording.characterID,
            out GameObject prefab))
        {
            Debug.LogWarning(
                $"[GHOST DATABASE - {ghostType}] Nenhum prefab encontrado " +
                $"para Character ID: {recording.characterID}"
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
                $"[GHOST DATABASE - {ghostType}] " +
                $"{prefab.name} não possui GhostPlayback."
            );

            Destroy(ghost);
            return null;
        }

        spawnedGhosts.Add(ghost);

        playback.Play(recording);

        Debug.Log(
            $"[GHOST DATABASE - {ghostType}] Ghost instanciado!\n" +
            $"Carro: {recording.characterID}\n" +
            $"Prefab: {prefab.name}\n" +
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
