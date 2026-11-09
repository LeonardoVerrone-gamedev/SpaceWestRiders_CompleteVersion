
using UnityEngine;
using System.Collections;

public class GhostPlayback : MonoBehaviour
{
    [Header("Visual References")]
    [SerializeField] private Transform carBody;
    [SerializeField] private Transform[] wheels = new Transform[4];

    [Header("Playback")]
    [SerializeField] private bool playOnStart = false;
    [SerializeField] private float playbackSpeed = 1f;

    private GhostRecordingData recording;
    private Coroutine playbackCoroutine;

    public bool IsPlaying { get; private set; }

    private void Start()
    {
        if (playOnStart && recording != null)
            Play(recording);
    }

    /// <summary>
    /// Recebe uma gravação e inicia sua reprodução.
    /// </summary>
    public void Play(GhostRecordingData data)
    {
        if (data == null || data.frames == null || data.frames.Count < 2)
        {
            Debug.LogWarning("GhostPlayback: gravação inválida.");
            return;
        }

        if (playbackSpeed <= 0f)
        {
            Debug.LogWarning("GhostPlayback: playbackSpeed deve ser maior que zero.");
            return;
        }

        recording = data;

        if (playbackCoroutine != null)
            StopCoroutine(playbackCoroutine);

        gameObject.SetActive(true);

        playbackCoroutine = StartCoroutine(PlaybackRoutine());
    }

    private IEnumerator PlaybackRoutine()
    {
        IsPlaying = true;

        float playbackTime = 0f;
        int currentFrameIndex = 0;

        ApplyFrame(recording.frames[0]);

        while (currentFrameIndex < recording.frames.Count - 1)
        {
            playbackTime += Time.deltaTime * playbackSpeed;

            // Avança até encontrar o próximo frame que contém o tempo.
            while (currentFrameIndex < recording.frames.Count - 2 &&
                   recording.frames[currentFrameIndex + 1].time < playbackTime)
            {
                currentFrameIndex++;
            }

            GhostFrameData from = recording.frames[currentFrameIndex];
            GhostFrameData to = recording.frames[currentFrameIndex + 1];

            float duration = to.time - from.time;

            float interpolation = duration > 0f
                ? Mathf.Clamp01((playbackTime - from.time) / duration)
                : 1f;

            ApplyInterpolatedFrame(from, to, interpolation);

            yield return null;
        }

        ApplyFrame(recording.frames[recording.frames.Count - 1]);

        IsPlaying = false;
        playbackCoroutine = null;
    }

    private void ApplyInterpolatedFrame(
        GhostFrameData from,
        GhostFrameData to,
        float t)
    {
        // Transform principal
        transform.position = Vector3.Lerp(
            from.position,
            to.position,
            t
        );

        transform.rotation = Quaternion.Slerp(
            from.rotation,
            to.rotation,
            t
        );

        // Carroceria
        if (carBody != null)
        {
            carBody.localPosition = Vector3.Lerp(
                from.bodyLocalPosition,
                to.bodyLocalPosition,
                t
            );

            carBody.localRotation = Quaternion.Slerp(
                from.bodyLocalRotation,
                to.bodyLocalRotation,
                t
            );
        }

        // Rodas
        ApplyInterpolatedWheels(from, to, t);
    }

    private void ApplyInterpolatedWheels(
        GhostFrameData from,
        GhostFrameData to,
        float t)
    {
        if (wheels == null)
            return;

        int count = Mathf.Min(
            wheels.Length,
            from.wheelLocalPositions != null
                ? from.wheelLocalPositions.Length : 0
        );

        count = Mathf.Min(
            count,
            to.wheelLocalPositions != null
                ? to.wheelLocalPositions.Length : 0
        );

        for (int i = 0; i < count; i++)
        {
            if (wheels[i] == null)
                continue;

            if (!IsWheelValid(from, i) || !IsWheelValid(to, i))
                continue;

            Vector3 localPosition = Vector3.Lerp(
                from.wheelLocalPositions[i],
                to.wheelLocalPositions[i],
                t
            );

            Quaternion localRotation = Quaternion.Slerp(
                from.wheelLocalRotations[i],
                to.wheelLocalRotations[i],
                t
            );

            wheels[i].position = transform.TransformPoint(localPosition);
            wheels[i].rotation = transform.rotation * localRotation;
        }
    }

    private void ApplyFrame(GhostFrameData frame)
    {
        transform.position = frame.position;
        transform.rotation = frame.rotation;

        if (carBody != null)
        {
            carBody.localPosition = frame.bodyLocalPosition;
            carBody.localRotation = frame.bodyLocalRotation;
        }

        if (wheels == null || frame.wheelLocalPositions == null)
            return;

        int count = Mathf.Min(
            wheels.Length,
            frame.wheelLocalPositions.Length
        );

        for (int i = 0; i < count; i++)
        {
            if (wheels[i] == null || !IsWheelValid(frame, i))
                continue;

            wheels[i].position = transform.TransformPoint(
                frame.wheelLocalPositions[i]
            );

            wheels[i].rotation = transform.rotation *
                frame.wheelLocalRotations[i];
        }
    }

    private bool IsWheelValid(GhostFrameData frame, int index)
    {
        return frame.wheelValid != null &&
               index < frame.wheelValid.Length &&
               frame.wheelValid[index] &&
               frame.wheelLocalRotations != null &&
               index < frame.wheelLocalRotations.Length;
    }

    public void StopPlayback()
    {
        if (playbackCoroutine != null)
        {
            StopCoroutine(playbackCoroutine);
            playbackCoroutine = null;
        }

        IsPlaying = false;
    }
}
