
using UnityEngine;
using System;
using System.IO;

public class LocalRecordMigration : MonoBehaviour
{
    private const string MIGRATION_KEY = "LOCAL_RECORD_MIGRATION_V1";

    private void Awake()
    {
        if (PlayerPrefs.GetInt(MIGRATION_KEY, 0) == 1)
            return;

        MigrateCircuit("Cave Circuit", "Nuzicäa");
        MigrateCircuit("Desert Track", "Tooine");
        MigrateCircuit("Ocean Circuit", "Aquopolis");

        PlayerPrefs.SetInt(MIGRATION_KEY, 1);
        PlayerPrefs.Save();

        Debug.Log("[LOCAL MIGRATION] Migração dos records locais concluída!");
    }

    private void MigrateCircuit(string oldID, string newID)
    {
        // 1. Migrar melhor tempo de corrida
        string oldRaceKey = "LOCAL_RACE_BEST_TIME_" + oldID;
        string newRaceKey = "LOCAL_RACE_BEST_TIME_" + newID;

        if (PlayerPrefs.HasKey(oldRaceKey))
        {
            float oldTime = PlayerPrefs.GetFloat(oldRaceKey);

            if (!PlayerPrefs.HasKey(newRaceKey) ||
                oldTime < PlayerPrefs.GetFloat(newRaceKey))
            {
                PlayerPrefs.SetFloat(newRaceKey, oldTime);
            }
        }

        // 2. Migrar melhor tempo de volta
        string oldLapKey = "LOCAL_BEST_LAP_TIME_" + oldID;
        string newLapKey = "LOCAL_BEST_LAP_TIME_" + newID;

        if (PlayerPrefs.HasKey(oldLapKey))
        {
            float oldTime = PlayerPrefs.GetFloat(oldLapKey);

            if (!PlayerPrefs.HasKey(newLapKey) ||
                oldTime < PlayerPrefs.GetFloat(newLapKey))
            {
                PlayerPrefs.SetFloat(newLapKey, oldTime);
            }
        }

        // 3. Migrar arquivo do Ghost
        string oldGhostPath = GetGhostPath(oldID);
        string newGhostPath = GetGhostPath(newID);

        if (File.Exists(oldGhostPath))
        {
            bool shouldCopy = !File.Exists(newGhostPath);

            if (!shouldCopy)
            {
                try
                {
                    string oldJson = File.ReadAllText(oldGhostPath);
                    string newJson = File.ReadAllText(newGhostPath);

                    GhostRecordingData oldGhost =
                        JsonUtility.FromJson<GhostRecordingData>(oldJson);

                    GhostRecordingData newGhost =
                        JsonUtility.FromJson<GhostRecordingData>(newJson);

                    int oldFrames = oldGhost?.frames?.Count ?? 0;
                    int newFrames = newGhost?.frames?.Count ?? 0;

                    shouldCopy = oldFrames > newFrames;
                }
                catch (Exception e)
                {
                    Debug.LogWarning(
                        $"[LOCAL MIGRATION] Erro comparando ghosts de {oldID}: {e.Message}"
                    );
                }
            }

            if (shouldCopy)
            {
                File.Copy(oldGhostPath, newGhostPath, true);

                Debug.Log(
                    $"[LOCAL MIGRATION] Ghost migrado: {oldID} -> {newID}"
                );
            }
        }

        Debug.Log(
            $"[LOCAL MIGRATION] Circuito migrado: {oldID} -> {newID}"
        );
    }

    private string GetGhostPath(string circuitID)
    {
        string safeID = string.Concat(
            circuitID.Split(Path.GetInvalidFileNameChars())
        );

        return Path.Combine(
            Application.persistentDataPath,
            "LOCAL_RACE_GHOST_" + safeID + ".json"
        );
    }
}
