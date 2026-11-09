
using UnityEngine;
using UnityEngine.Networking;
using System;
using System.Collections;
using System.Collections.Generic;

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
