
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;

public class SupabaseGhostService : MonoBehaviour
{
    public static SupabaseGhostService Instance { get; private set; }

    [Header("Supabase Configuration")]
    [SerializeField] private string supabaseUrl;
    [SerializeField] private string supabaseAnonKey;

    [Header("Request Settings")]
    [SerializeField] private int requestTimeout = 10;

    private const string UsernamePrefsKey = "PlayerUsername";
    private const string UsernameSyncedPrefsKey = "PlayerUsernameSynced";

    private bool usernameSyncInProgress;

    public static string SavedUsername =>
        PlayerPrefs.GetString(UsernamePrefsKey, "").Trim();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // =====================================================
    // BUSCAR MELHOR RECORD + GHOST
    // =====================================================

    public void GetBestRaceRecord(
        string circuitID,
        Action<BestRaceRecordResult> onComplete)
    {
        StartCoroutine(
            FetchBestRaceRecordCoroutine(circuitID, onComplete)
        );
    }

    private IEnumerator FetchBestRaceRecordCoroutine(
        string circuitID,
        Action<BestRaceRecordResult> onComplete)
    {
        if (string.IsNullOrWhiteSpace(circuitID))
        {
            Debug.LogError("[SUPABASE] Circuit ID inválido.");
            onComplete?.Invoke(null);
            yield break;
        }

        string encodedCircuitID =
            UnityWebRequest.EscapeURL(circuitID);

        string url =
            $"{supabaseUrl}/rest/v1/race_records" +
            $"?select=id,circuit_id,circuit_name,player_id,player_name,username,total_time,created_at,car_character_id" +
            $"&circuit_id=eq.{encodedCircuitID}" +
            $"&order=total_time.asc" +
            $"&limit=1";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            ConfigureRequest(request);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError(
                    $"[SUPABASE] Erro buscando recorde: {request.error}\n" +
                    request.downloadHandler.text
                );

                onComplete?.Invoke(null);
                yield break;
            }

            SupabaseArrayResponse<BestRaceRecordRow> response;

            try
            {
                string json =
                    "{\"items\":" + request.downloadHandler.text + "}";

                response = JsonUtility.FromJson<
                    SupabaseArrayResponse<BestRaceRecordRow>
                >(json);
            }
            catch (Exception e)
            {
                Debug.LogError(
                    $"[SUPABASE] Erro ao interpretar recorde: {e.Message}"
                );

                onComplete?.Invoke(null);
                yield break;
            }

            if (response == null ||
                response.items == null ||
                response.items.Count == 0)
            {
                Debug.Log(
                    $"[SUPABASE] Nenhum recorde encontrado para {circuitID}."
                );

                onComplete?.Invoke(null);
                yield break;
            }

            BestRaceRecordRow bestRecord = response.items[0];

            Debug.Log(
                $"[SUPABASE] Recorde encontrado: " +
                $"{bestRecord.player_name} - {bestRecord.total_time:F3}s"
            );

            // Buscar ghost vinculado ao ID do recorde.
            yield return FetchGhostForRecord(bestRecord, onComplete);
        }
    }

    // =====================================================
    // BUSCAR GHOST VINCULADO AO RECORD
    // =====================================================

    private IEnumerator FetchGhostForRecord(
        BestRaceRecordRow record,
        Action<BestRaceRecordResult> onComplete)
    {
        string encodedRecordID =
            UnityWebRequest.EscapeURL(record.id);

        string url =
            $"{supabaseUrl}/rest/v1/ghost_data" +
            $"?select=frame_data" +
            $"&race_record_id=eq.{encodedRecordID}" +
            $"&limit=1";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            ConfigureRequest(request);

            yield return request.SendWebRequest();

            BestRaceRecordResult result =
                new BestRaceRecordResult
                {
                    record = record,
                    ghost = null
                };

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning(
                    $"[SUPABASE] Erro buscando ghost: {request.error}\n" +
                    request.downloadHandler.text
                );

                onComplete?.Invoke(result);
                yield break;
            }

            try
            {
                string json =
                    "{\"items\":" + request.downloadHandler.text + "}";

                var response = JsonUtility.FromJson<
                    SupabaseArrayResponse<GhostFrameDataRow>
                >(json);

                if (response != null &&
                    response.items != null &&
                    response.items.Count > 0)
                {
                    result.ghost = response.items[0].frame_data;
                }
            }
            catch (Exception e)
            {
                Debug.LogError(
                    $"[SUPABASE] Erro interpretando ghost: {e.Message}"
                );
            }

            if (result.ghost != null)
            {
                Debug.Log(
                    $"[SUPABASE] Ghost carregado: " +
                    $"{result.ghost.frames?.Count ?? 0} frames."
                );
            }
            else
            {
                Debug.LogWarning(
                    "[SUPABASE] Recorde encontrado, mas sem ghost válido."
                );
            }

            onComplete?.Invoke(result);
        }
    }

    // =====================================================
    // CONFIGURAÇÃO DAS REQUESTS
    // =====================================================

    private void ConfigureRequest(UnityWebRequest request)
    {
        request.timeout = requestTimeout;

        request.SetRequestHeader("apikey", supabaseAnonKey);
        request.SetRequestHeader(
            "Authorization",
            $"Bearer {supabaseAnonKey}"
        );

        request.SetRequestHeader("Accept", "application/json");
    }

    // =====================================================
    // SINCRONIZAR USERNAME COM SUPABASE
    // =====================================================

    public void SyncSavedUsername(Action<bool> onComplete = null)
    {
        if (usernameSyncInProgress)
            return;

        string username = SavedUsername;

        if (string.IsNullOrWhiteSpace(username))
        {
            Debug.LogWarning("[SUPABASE] Nenhum username salvo.");
            onComplete?.Invoke(false);
            return;
        }

        // Já foi sincronizado anteriormente.
        if (PlayerPrefs.GetInt(UsernameSyncedPrefsKey, 0) == 1)
        {
            onComplete?.Invoke(true);
            return;
        }

        string playerID = GameManagerInstance.Instance != null
            ? GameManagerInstance.Instance.PlayerId
            : "";

        if (!Guid.TryParse(playerID, out _))
        {
            Debug.LogWarning(
                "[SUPABASE] PlayerId inválido. Sincronização adiada."
            );

            onComplete?.Invoke(false);
            return;
        }

        var body = new UsernameSyncRequest
        {
            p_player_id = playerID,
            p_username = username
        };

        string json = JsonUtility.ToJson(body);

        usernameSyncInProgress = true;

        Debug.Log(
            $"[SUPABASE] Sincronizando username: {username}"
        );

        StartCoroutine(
            PostRpc("update_player_username", json, response =>
            {
                usernameSyncInProgress = false;

                if (string.IsNullOrWhiteSpace(response))
                {
                    Debug.LogWarning(
                        "[SUPABASE] Falha na sincronização do username. " +
                        "Será tentada novamente."
                    );

                    onComplete?.Invoke(false);
                    return;
                }

                try
                {
                    UsernameSyncResponse result =
                        JsonUtility.FromJson<UsernameSyncResponse>(response);

                    if (result == null || !result.success)
                    {
                        Debug.LogWarning(
                            "[SUPABASE] Resposta inválida na sincronização."
                        );

                        onComplete?.Invoke(false);
                        return;
                    }

                    // Só marca como sincronizado após confirmação do Supabase.
                    PlayerPrefs.SetInt(UsernameSyncedPrefsKey, 1);
                    PlayerPrefs.Save();

                    Debug.Log(
                        $"[SUPABASE] Username sincronizado!\n" +
                        $"Corridas atualizadas: {result.race_records_updated}\n" +
                        $"Voltas atualizadas: {result.lap_records_updated}"
                    );

                    onComplete?.Invoke(true);
                }
                catch (Exception e)
                {
                    Debug.LogError(
                        "[SUPABASE] Erro interpretando sincronização: " +
                        e.Message
                    );

                    onComplete?.Invoke(false);
                }
            })
        );
    }

    // =====================================================
    // ENVIAR RECORD DE CORRIDA + GHOST
    // =====================================================

    public void SubmitRaceRecord(
        GhostRecordingData ghostData,
        string circuitID,
        string circuitName,
        string playerName)
    {
        if (ghostData == null || ghostData.frames == null ||
            ghostData.frames.Count < 2)
        {
            Debug.LogWarning("[SUPABASE] Ghost inválido. Envio cancelado.");
            return;
        }

        string playerID = GameManagerInstance.Instance != null
            ? GameManagerInstance.Instance.PlayerId
            : "";

                string username = SavedUsername;

                if (!Guid.TryParse(playerID, out _))
                {
                    Debug.LogError("[SUPABASE] PlayerId inválido.");
                    return;
                }

                if (string.IsNullOrWhiteSpace(username))
                {
                    Debug.LogError(
                        "[SUPABASE] Username não cadastrado. " +
                        "Envio de recorde cancelado."
                    );
                    return;
                }

        if (!Guid.TryParse(playerID, out _))
        {
            Debug.LogError(
                "[SUPABASE] PlayerId ausente ou inválido. " +
                "Não foi possível enviar o recorde."
            );
            return;
        }

        var body = new RaceRecordRpcRequest
        {
            p_circuit_id = circuitID,
            p_circuit_name = circuitName,
            p_player_id = playerID,
            p_player_name = username,
            p_car_character_id = ghostData.characterID,
            p_total_time = ghostData.totalTime,
            p_ghost_data = ghostData
        };

        string json = JsonUtility.ToJson(body);

        Debug.Log(
            $"[SUPABASE] Enviando recorde e Ghost: " +
            $"{username} - {ghostData.totalTime:F3}s"
        );

        StartCoroutine(
            PostRpc("submit_race_record", json, response =>
            {
                string recordID = ParseRpcUUID(response);

                if (recordID == null)
                {
                    Debug.Log(
                        "[SUPABASE] Corrida enviada, mas não entrou no Top 10."
                    );
                    return;
                }

                Debug.Log(
                    $"[SUPABASE] Recorde e Ghost salvos! ID: {recordID}"
                );
            })
        );
    }


    // =====================================================
    // ENVIAR MELHOR VOLTA
    // =====================================================

    public void SubmitLapRecord(
        string circuitID,
        string circuitName,
        string playerName,
        float lapTime)
    {
        string playerID = GameManagerInstance.Instance != null
            ? GameManagerInstance.Instance.PlayerId
            : "";

        string username = SavedUsername;

        if (!Guid.TryParse(playerID, out _))
        {
            Debug.LogError("[SUPABASE] PlayerId inválido.");
            return;
        }

        if (string.IsNullOrWhiteSpace(username))
        {
            Debug.LogError("[SUPABASE] Username não cadastrado.");
            return;
        }

        if (!Guid.TryParse(playerID, out _))
        {
            Debug.LogError(
                "[SUPABASE] PlayerId ausente ou inválido. " +
                "Não foi possível enviar a volta."
            );
            return;
        }

        if (string.IsNullOrWhiteSpace(circuitID) || lapTime <= 0f)
            return;

        var body = new LapRecordRpcRequest
        {
            p_circuit_id = circuitID,
            p_circuit_name = circuitName,
            p_player_id = playerID,
            p_player_name = username,
            p_lap_time = lapTime
        };

        string json = JsonUtility.ToJson(body);

        StartCoroutine(
            PostRpc("submit_lap_record", json, response =>
            {
                string lapRecordID = ParseRpcUUID(response);

                if (lapRecordID == null)
                {
                    Debug.Log(
                        "[SUPABASE] Volta enviada, mas não permaneceu no Top 10."
                    );
                    return;
                }

                Debug.Log(
                    $"[SUPABASE] Volta registrada no Top 10! " +
                    $"Tempo: {lapTime:F3}s | ID: {lapRecordID}"
                );
            })
        );
    }

    // =====================================================
    // BUSCAR MELHOR VOLTA GLOBAL
    // =====================================================

    public void GetBestLapRecord(
        string circuitID,
        Action<float, bool> onComplete)
    {
        StartCoroutine(
            FetchBestLapRecordCoroutine(circuitID, onComplete)
        );
    }

    // =====================================================
    // BUSCAR TOP 3 GLOBAL DE CORRIDAS
    // =====================================================

    public void GetTop3RaceRecords(
        string circuitID,
        Action<List<GlobalRaceRecordRow>, bool> onComplete)
    {
        StartCoroutine(
            FetchTop3RaceRecordsCoroutine(circuitID, onComplete)
        );
    }

    private IEnumerator FetchTop3RaceRecordsCoroutine(
        string circuitID,
        Action<List<GlobalRaceRecordRow>, bool> onComplete)
    {
        if (string.IsNullOrWhiteSpace(circuitID))
        {
            onComplete?.Invoke(null, false);
            yield break;
        }

        string encodedCircuitID =
            UnityWebRequest.EscapeURL(circuitID);

        string url =
            $"{supabaseUrl}/rest/v1/race_records" +
            $"?select=username,player_name,total_time" +
            $"&circuit_id=eq.{encodedCircuitID}" +
            $"&order=total_time.asc,created_at.asc" +
            $"&limit=3";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            ConfigureRequest(request);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError(
                    "[SUPABASE] Erro buscando Top 3:\n" +
                    request.downloadHandler.text
                );

                onComplete?.Invoke(null, false);
                yield break;
            }

            try
            {
                string json =
                    "{\"items\":" + request.downloadHandler.text + "}";

                var response = JsonUtility.FromJson<
                    SupabaseArrayResponse<GlobalRaceRecordRow>
                >(json);

                List<GlobalRaceRecordRow> records =
                    response?.items ?? new List<GlobalRaceRecordRow>();

                onComplete?.Invoke(records, true);
            }
            catch (Exception e)
            {
                Debug.LogError(
                    "[SUPABASE] Erro interpretando Top 3: " + e.Message
                );

                onComplete?.Invoke(null, false);
            }
        }
    }

    // =====================================================
    // BUSCAR MELHOR VOLTA GLOBAL + USERNAME
    // =====================================================

    public void GetBestGlobalLapRecord(
        string circuitID,
        Action<BestLapRecordRow, bool> onComplete)
    {
        StartCoroutine(
            FetchBestGlobalLapRecordCoroutine(circuitID, onComplete)
        );
    }

    private IEnumerator FetchBestGlobalLapRecordCoroutine(
        string circuitID,
        Action<BestLapRecordRow, bool> onComplete)
    {
        if (string.IsNullOrWhiteSpace(circuitID))
        {
            onComplete?.Invoke(null, false);
            yield break;
        }

        string encodedCircuitID =
            UnityWebRequest.EscapeURL(circuitID);

        string url =
            $"{supabaseUrl}/rest/v1/lap_records" +
            $"?select=username,player_name,lap_time" +
            $"&circuit_id=eq.{encodedCircuitID}" +
            $"&order=lap_time.asc,created_at.asc" +
            $"&limit=1";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            ConfigureRequest(request);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError(
                    "[SUPABASE] Erro buscando melhor volta global:\n" +
                    request.downloadHandler.text
                );

                onComplete?.Invoke(null, false);
                yield break;
            }

            try
            {
                string json =
                    "{\"items\":" + request.downloadHandler.text + "}";

                var response = JsonUtility.FromJson<
                    SupabaseArrayResponse<BestLapRecordRow>
                >(json);

                if (response == null ||
                    response.items == null ||
                    response.items.Count == 0)
                {
                    onComplete?.Invoke(null, true);
                    yield break;
                }

                onComplete?.Invoke(response.items[0], true);
            }
            catch (Exception e)
            {
                Debug.LogError(
                    "[SUPABASE] Erro interpretando melhor volta: " +
                    e.Message
                );

                onComplete?.Invoke(null, false);
            }
        }
    }

    private IEnumerator FetchBestLapRecordCoroutine(
        string circuitID,
        Action<float, bool> onComplete)
    {
        if (string.IsNullOrWhiteSpace(circuitID))
        {
            onComplete?.Invoke(float.MaxValue, false);
            yield break;
        }

        string encodedCircuitID =
            UnityWebRequest.EscapeURL(circuitID);

        string url =
            $"{supabaseUrl}/rest/v1/lap_records" +
            $"?select=lap_time" +
            $"&circuit_id=eq.{encodedCircuitID}" +
            $"&order=lap_time.asc" +
            $"&limit=1";

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            ConfigureRequest(request);

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError(
                    $"[SUPABASE] Erro buscando melhor volta: " +
                    $"{request.error}\n{request.downloadHandler.text}"
                );

                // false significa que não conseguimos consultar.
                onComplete?.Invoke(float.MaxValue, false);
                yield break;
            }

            try
            {
                string json =
                    "{\"items\":" + request.downloadHandler.text + "}";

                var response = JsonUtility.FromJson<
                    SupabaseArrayResponse<BestLapRecordRow>
                >(json);

                if (response == null ||
                    response.items == null ||
                    response.items.Count == 0)
                {
                    Debug.Log(
                        $"[SUPABASE] Nenhuma volta registrada para {circuitID}."
                    );

                    // Consulta funcionou, mas ainda não existe recorde.
                    onComplete?.Invoke(float.MaxValue, true);
                    yield break;
                }

                float bestLap = response.items[0].lap_time;

                Debug.Log(
                    $"[SUPABASE] Melhor volta global: {bestLap:F3}s"
                );

                onComplete?.Invoke(bestLap, true);
            }
            catch (Exception e)
            {
                Debug.LogError(
                    $"[SUPABASE] Erro interpretando melhor volta: {e.Message}"
                );

                onComplete?.Invoke(float.MaxValue, false);
            }
        }
    }


    // =====================================================
    // REQUISIÇÃO RPC
    // =====================================================

    private IEnumerator PostRpc(
        string functionName,
        string jsonBody,
        Action<string> onComplete)
    {
        string baseURL = supabaseUrl.TrimEnd('/');

        string url =
            $"{baseURL}/rest/v1/rpc/{functionName}";

        using (UnityWebRequest request =
            new UnityWebRequest(url, "POST"))
        {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);

            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();

            ConfigureRequest(request);

            request.SetRequestHeader(
                "Content-Type",
                "application/json"
            );

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError(
                    $"[SUPABASE] Erro na RPC {functionName}:\n" +
                    $"HTTP: {request.responseCode}\n" +
                    $"Erro: {request.error}\n" +
                    $"Resposta: {request.downloadHandler.text}"
                );

                onComplete?.Invoke(null);
                yield break;
            }

            string response = request.downloadHandler.text;

            Debug.Log(
                $"[SUPABASE] RPC {functionName} concluída. " +
                $"Resposta: {response}"
            );

            onComplete?.Invoke(response);
        }
    }


    // =====================================================
    // INTERPRETAR UUID RETORNADO PELA RPC
    // =====================================================

    private string ParseRpcUUID(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return null;

        string value = response.Trim();

        if (value == "null")
            return null;

        value = value.Trim('"');

        return Guid.TryParse(value, out _)
            ? value
            : null;
    }
}

// =========================================================
// MODELOS DE RESPOSTA
// =========================================================

[Serializable]
public class BestRaceRecordRow
{
    public string id;
    public string circuit_id;
    public string circuit_name;
    public string player_id;
    public string player_name;
    public string username;
    public float total_time;
    public string created_at;
    public string car_character_id;
}

[Serializable]
public class GlobalRaceRecordRow
{
    public string username;
    public string player_name;
    public float total_time;
}

[Serializable]
public class GhostFrameDataRow
{
    public GhostRecordingData frame_data;
}

[Serializable]
public class BestRaceRecordResult
{
    public BestRaceRecordRow record;
    public GhostRecordingData ghost;
}

[Serializable]
public class SupabaseArrayResponse<T>
{
    public List<T> items;
}

[Serializable]
public class RaceRecordRpcRequest
{
    public string p_circuit_id;
    public string p_circuit_name;
    public string p_player_id;
    public string p_player_name;
    public string p_car_character_id;
    public float p_total_time;
    public GhostRecordingData p_ghost_data;
}

[Serializable]
public class UsernameSyncRequest
{
    public string p_player_id;
    public string p_username;
}

[Serializable]
public class UsernameSyncResponse
{
    public bool success;
    public int race_records_updated;
    public int lap_records_updated;
}

[Serializable]
public class LapRecordRpcRequest
{
    public string p_circuit_id;
    public string p_circuit_name;
    public string p_player_id;
    public string p_player_name;
    public float p_lap_time;
}

[Serializable]
public class BestLapRecordRow
{
    public string username;
    public string player_name;
    public float lap_time;
}