using UnityEngine;
using System.Collections.Generic;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class RacingLineRecorder : MonoBehaviour
{
    [Header("Recording Settings")]
    [SerializeField] private float recordingInterval = 0.05f; // 20Hz
    [SerializeField] private string racingLineName = "MyRacingLine";
    
    private bool isRecording = false;
    private float timer = 0f;
    private List<TransformData> recordedPoints = new List<TransformData>();
    
    [System.Serializable]
    public class TransformData
    {
        public Vector3 position;
        public Quaternion rotation;
        public float speed;
        
        public TransformData(Vector3 pos, Quaternion rot, float spd)
        {
            position = pos;
            rotation = rot;
            speed = spd;
        }
    }
    
    #if UNITY_EDITOR
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.G))
        {
            ToggleRecording();
        }
        
        if (isRecording)
        {
            timer += Time.deltaTime;
            if (timer >= recordingInterval)
            {
                timer = 0f;
                RecordPoint();
            }
        }
    }
    
    void ToggleRecording()
    {
        if (!isRecording)
        {
            StartRecording();
        }
        else
        {
            StopAndSaveRecording();
        }
    }
    
    void StartRecording()
    {
        isRecording = true;
        recordedPoints.Clear();
        Debug.Log("🎥 Iniciando gravação da Racing Line...");
    }
    
    void RecordPoint()
    {
        var carPhysics = GetComponent<SCR_RayBasedCarPhysics>();
        float currentSpeed = carPhysics != null ? carPhysics.GetCurrentSpeed() : 0f;
        
        recordedPoints.Add(new TransformData(
            transform.position,
            transform.rotation,
            currentSpeed
        ));
    }
    
    void StopAndSaveRecording()
    {
        isRecording = false;
        
        // Criar GameObject da racing line
        GameObject racingLine = new GameObject(racingLineName);
        
        // Criar children para cada ponto
        for (int i = 0; i < recordedPoints.Count; i++)
        {
            GameObject point = new GameObject($"Point_{i:0000}");
            point.transform.position = recordedPoints[i].position;
            point.transform.rotation = recordedPoints[i].rotation;
            point.transform.SetParent(racingLine.transform);
            
            // Adicionar componente com dados
            var data = point.AddComponent<RacingPointData>();
            data.speed = recordedPoints[i].speed;
            data.index = i;
        }
        
        // Salvar como prefab
        string folderPath = "Assets/RacingLines";
        if (!System.IO.Directory.Exists(folderPath))
            System.IO.Directory.CreateDirectory(folderPath);
        
        string prefabPath = $"{folderPath}/{racingLineName}.prefab";
        PrefabUtility.SaveAsPrefabAsset(racingLine, prefabPath);
        
        DestroyImmediate(racingLine);
        Debug.Log($"✅ Racing Line salva: {recordedPoints.Count} pontos em {prefabPath}");
    }
    #endif
}

public class RacingPointData : MonoBehaviour
{
    public float speed;
    public int index;
}