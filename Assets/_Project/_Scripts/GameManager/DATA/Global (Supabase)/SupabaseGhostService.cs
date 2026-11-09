
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
            $"?select=id,circuit_id,circuit_name,player_id,player_name,total_time,created_at,car_character_id" +
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
            p_player_name = playerName,
            p_car_character_id = ghostData.characterID,
            p_total_time = ghostData.totalTime,
            p_ghost_data = ghostData
        };

        string json = JsonUtility.ToJson(body);

        Debug.Log(
            $"[SUPABASE] Enviando recorde e Ghost: " +
            $"{playerName} - {ghostData.totalTime:F3}s"
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
            p_player_name = playerName,
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
    public float total_time;
    public string created_at;
    public string car_character_id;
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
public class LapRecordRpcRequest
{
    public string p_circuit_id;
    public string p_circuit_name;
    public string p_player_id;
    public string p_player_name;
    public float p_lap_time;
}