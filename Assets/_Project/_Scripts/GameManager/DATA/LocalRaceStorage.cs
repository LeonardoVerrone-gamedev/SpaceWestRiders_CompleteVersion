
using UnityEngine;
using System;
using System.IO;

public static class LocalRaceRecordStorage
{
    private const string BEST_TIME_PREFIX = "LOCAL_RACE_BEST_TIME_";
    private const string GHOST_PREFIX = "LOCAL_RACE_GHOST_";

    private const string BEST_LAP_PREFIX = "LOCAL_BEST_LAP_TIME_";

    private static string GetTimeKey(string circuitID)
    {
        return BEST_TIME_PREFIX + circuitID;
    }

    private static string GetGhostPath(string circuitID)
    {
        string safeID = string.Concat(
            circuitID.Split(Path.GetInvalidFileNameChars())
        );

        return Path.Combine(
            Application.persistentDataPath,
            GHOST_PREFIX + safeID + ".json"
        );
    }

    public static float GetBestRaceTime(string circuitID)
    {
        if (string.IsNullOrEmpty(circuitID))
            return float.MaxValue;

        return PlayerPrefs.GetFloat(
            GetTimeKey(circuitID),
            float.MaxValue
        );
    }

    public static bool SaveIfBetter(
        string circuitID,
        float totalTime,
        GhostRecordingData ghostData)
    {
        if (string.IsNullOrEmpty(circuitID))
        {
            Debug.LogError("[LOCAL RECORD] Circuit ID inválido.");
            return false;
        }

        if (totalTime <= 0f || ghostData == null ||
            ghostData.frames == null || ghostData.frames.Count < 2)
        {
            Debug.LogWarning(
                "[LOCAL RECORD] Tempo ou Ghost inválido. Registro não salvo."
            );
            return false;
        }

        float previousBest = GetBestRaceTime(circuitID);

        if (totalTime >= previousBest)
        {
            Debug.Log(
                $"[LOCAL RECORD] Recorde mantido: {FormatTime(previousBest)}"
            );
            return false;
        }

        string ghostPath = GetGhostPath(circuitID);
        string temporaryPath = ghostPath + ".tmp";

        try
        {
            string json = JsonUtility.ToJson(ghostData);

            // Primeiro salva o Ghost em arquivo temporário.
            File.WriteAllText(temporaryPath, json);

            // Substitui o Ghost anterior.
            File.Copy(temporaryPath, ghostPath, true);
            File.Delete(temporaryPath);

            // Só atualiza o tempo depois de salvar o Ghost.
            PlayerPrefs.SetFloat(
                GetTimeKey(circuitID),
                totalTime
            );

            PlayerPrefs.Save();

            Debug.Log(
                $"[LOCAL RECORD] NOVO RECORDE!\n" +
                $"Circuito: {circuitID}\n" +
                $"Tempo: {FormatTime(totalTime)}\n" +
                $"Ghost salvo em: {ghostPath}"
            );

            return true;
        }
        catch (Exception e)
        {
            Debug.LogError(
                $"[LOCAL RECORD] Erro ao salvar recorde: {e.Message}"
            );

            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);

            return false;
        }
    }

    public static GhostRecordingData LoadGhost(string circuitID)
    {
        if (string.IsNullOrEmpty(circuitID))
            return null;

        string path = GetGhostPath(circuitID);

        if (!File.Exists(path))
        {
            Debug.Log(
                $"[LOCAL RECORD] Nenhum Ghost local encontrado para {circuitID}."
            );

            return null;
        }

        try
        {
            string json = File.ReadAllText(path);

            GhostRecordingData ghost =
                JsonUtility.FromJson<GhostRecordingData>(json);

            if (ghost == null || ghost.frames == null ||
                ghost.frames.Count < 2)
            {
                Debug.LogWarning(
                    "[LOCAL RECORD] Arquivo de Ghost inválido."
                );

                return null;
            }

            return ghost;
        }
        catch (Exception e)
        {
            Debug.LogError(
                $"[LOCAL RECORD] Erro ao carregar Ghost: {e.Message}"
            );

            return null;
        }
    }

    private static string FormatTime(float time)
    {
        if (time == float.MaxValue)
            return "--:--.---";

        int minutes = Mathf.FloorToInt(time / 60f);
        int seconds = Mathf.FloorToInt(time % 60f);
        int milliseconds = Mathf.FloorToInt((time * 1000f) % 1000f);

        return $"{minutes:00}:{seconds:00}.{milliseconds:000}";
    }

    private static string GetLapTimeKey(string circuitID)
    {
        return BEST_LAP_PREFIX + circuitID;
    }

    public static float GetBestLapTime(string circuitID)
    {
        if (string.IsNullOrEmpty(circuitID))
            return float.MaxValue;

        return PlayerPrefs.GetFloat(
            GetLapTimeKey(circuitID),
            float.MaxValue
        );
    }

    public static bool SaveBestLapIfBetter(
        string circuitID,
        float lapTime)
    {
        if (string.IsNullOrEmpty(circuitID) ||
            lapTime <= 0f ||
            float.IsNaN(lapTime) ||
            float.IsInfinity(lapTime))
        {
            return false;
        }

        float previousBest = GetBestLapTime(circuitID);

        if (lapTime >= previousBest)
            return false;

        PlayerPrefs.SetFloat(GetLapTimeKey(circuitID), lapTime);
        PlayerPrefs.Save();

        Debug.Log(
            $"[LOCAL LAP RECORD] Nova melhor volta pessoal!\n" +
            $"Circuito: {circuitID}\n" +
            $"Tempo: {lapTime:F3}s"
        );

        return true;
    }
}
