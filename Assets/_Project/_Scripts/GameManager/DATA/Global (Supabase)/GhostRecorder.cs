
using UnityEngine;
using System;
using System.Collections.Generic;

[Serializable]
public class GhostRecordingData
{
    public int version = 1;
    public float sampleInterval;
    public string characterID;
    public float totalTime;
    public List<GhostFrameData> frames = new List<GhostFrameData>();
}

[Serializable]
public class GhostFrameData
{
    public float time;

    // Transform principal do carro
    public Vector3 position;
    public Quaternion rotation;

    // Transform visual da carroceria
    public Vector3 bodyLocalPosition;
    public Quaternion bodyLocalRotation;

    // Posição e rotação das rodas relativas ao carro
    public Vector3[] wheelLocalPositions;
    public Quaternion[] wheelLocalRotations;
    public bool[] wheelValid;
}

[DefaultExecutionOrder(1000)]
public class GhostRecorder : MonoBehaviour
{
    [Header("Recording Settings")]
    [SerializeField, Min(0.01f)]
    private float sampleInterval = 0.05f;

    private RacerStatus racerStatus;
    private SCR_RayBasedCarPhysics carPhysics;
    private SCR_CarIdentity carIdentity;

    private GhostRecordingData recording;

    private bool isRecording;
    private bool hasFinished;

    private float raceStartTime;
    private float nextSampleTime;

    public bool IsRecording => isRecording;
    public bool HasFinished => hasFinished;

    public GhostRecordingData Recording => recording;

    private void Awake()
    {
        racerStatus = GetComponent<RacerStatus>();
        carPhysics = GetComponent<SCR_RayBasedCarPhysics>();
        carIdentity = GetComponent<SCR_CarIdentity>();

        if (racerStatus == null)
            Debug.LogError($"GhostRecorder: RacerStatus não encontrado em {name}");

        if (carPhysics == null)
            Debug.LogError($"GhostRecorder: SCR_RayBasedCarPhysics não encontrado em {name}");

        if (carIdentity == null || carIdentity.racerData == null)
            Debug.LogError($"GhostRecorder: SCR_CarIdentity ou racerData não encontrado em {name}");
    }

    private void OnEnable()
    {
        RaceManager.OnRaceEffectiveStart += StartRecording;
        RaceManager.OnPlayerRacerFinished += FinishRecording;
    }

    private void OnDisable()
    {
        RaceManager.OnRaceEffectiveStart -= StartRecording;
        RaceManager.OnPlayerRacerFinished -= FinishRecording;

        isRecording = false;
    }

    private void StartRecording()
    {
        if (racerStatus == null || !racerStatus.isPlayer)
            return;

        if (carPhysics == null || carIdentity == null ||
            carIdentity.racerData == null)
            return;

        recording = new GhostRecordingData
        {
            sampleInterval = sampleInterval,
            characterID = carIdentity.racerData.characterID
        };

        raceStartTime = Time.time;
        nextSampleTime = 0f;

        isRecording = true;
        hasFinished = false;

        // Captura o estado inicial do carro.
        CaptureFrame(0f);

        Debug.Log($"GhostRecorder: gravação iniciada para {name}");
    }

    private void FixedUpdate()
    {
        if (!isRecording || recording == null)
            return;

        float elapsedTime = Time.time - raceStartTime;

        if (elapsedTime < nextSampleTime)
            return;

        CaptureFrame(elapsedTime);

        nextSampleTime = elapsedTime + sampleInterval;
    }

    private void CaptureFrame(float elapsedTime)
    {
        if (carPhysics == null)
            return;

        GhostFrameData frame = carPhysics.CaptureGhostFrame(elapsedTime);

        if (frame != null)
            recording.frames.Add(frame);
    }

    private void FinishRecording(RacerStatus racer, float totalTime)
    {
        if (racer != racerStatus || !isRecording)
            return;

        // Captura o último estado exatamente no encerramento.
        CaptureFrame(totalTime);

        recording.totalTime = totalTime;

        isRecording = false;
        hasFinished = true;

        Debug.Log(
            $"GhostRecorder: gravação finalizada!\n" +
            $"Carro: {recording.characterID}\n" +
            $"Tempo: {recording.totalTime:F3}s\n" +
            $"Frames: {recording.frames.Count}"
        );

        //Debug.Log($"Ghost JSON: {GetRecordingJson()}");
    }

    public string GetRecordingJson()
    {
        if (recording == null || !hasFinished)
            return null;

        return JsonUtility.ToJson(recording);
    }

    public void ClearRecording()
    {
        isRecording = false;
        hasFinished = false;
        recording = null;
    }
}
