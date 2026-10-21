
using System;
using System.Collections.Generic;
using UnityEngine;

public class SCR_ArcadeRaceAudioManager : MonoBehaviour
{
    // =========================================================
    // PERFIS DE ÁUDIO
    // Configurados no Inspector e compartilhados por tipo de carro.
    // =========================================================

    [Serializable]
    public class CarAudioClips
    {
        [Header("Motor")]
        public AudioClip engineLoop;
        [Range(0f, 1f)] public float engineVolume = 0.8f;
        public float minPitch = 0.7f;
        public float maxPitch = 1.6f;

        [Header("Derrapagem")]
        public AudioClip driftLoop;
        [Range(0f, 1f)] public float driftVolume = 0.5f;

        [Header("Efeitos do carro")]
        public AudioClip gearShift;
        public AudioClip turboStart;
        public AudioClip collision;
        public AudioClip heavyCollision;
        public AudioClip landing;
    }

    [Header("Perfis de áudio por tipo de carro")]
    [SerializeField] private CarAudioClips hoverClips = new CarAudioClips();
    [SerializeField] private CarAudioClips classicClips = new CarAudioClips();

    [Serializable]
    private class CarAudioSetup
    {
        public SCR_RayBasedCarPhysics car;
        [NonSerialized] public CarAudioClips activeClips;
    }

    [Serializable]
    private class EffectVoice
    {
        public AudioSource source;
        public int priority;
    }

    private const int MAX_HUMAN_PLAYERS = 2;

    [Header("Mixagem dos motores")]
    [Range(0f, 1f)]
    [SerializeField] private float secondaryEngineVolume = 0.18f;

    [SerializeField] private float dominantSwitchThreshold = 0.12f;
    [SerializeField] private float volumeTransitionSpeed = 3f;
    [SerializeField] private float pitchTransitionSpeed = 5f;

    [Header("Mixagem dos efeitos")]
    [Min(1)]
    [SerializeField] private int effectVoiceCount = 6;

    [Range(0f, 1f)]
    [SerializeField] private float effectsMasterVolume = 1f;

    [Range(0f, 1f)]
    [SerializeField] private float collisionHeavyThreshold = 0.8f;

    private CarAudioSetup[] players = Array.Empty<CarAudioSetup>();

    private AudioSource[] engineSources = Array.Empty<AudioSource>();
    private AudioSource[] driftSources = Array.Empty<AudioSource>();
    private EffectVoice[] effectVoices = Array.Empty<EffectVoice>();
    private float[] targetEngineVolumes = Array.Empty<float>();

    private Action[] gearHandlers;
    private Action[] turboHandlers;
    private Action<float, Vector3>[] collisionHandlers;
    private Action<float>[] landingHandlers;

    private int dominantPlayer;
    private bool isInitialized;
    private bool eventsSubscribed;

    private const int PRIORITY_GEAR = 2;
    private const int PRIORITY_DRIFT = 3;
    private const int PRIORITY_TURBO = 5;
    private const int PRIORITY_COLLISION = 1;
    private const int PRIORITY_HEAVY_COLLISION = 3;


    public static SCR_ArcadeRaceAudioManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning(
                "[ArcadeRaceAudioManager] Já existe uma instância. " +
                "Este componente duplicado será removido.",
                this);

            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        UnsubscribeFromAllCars();

        if (Instance == this)
            Instance = null;
    }

    private void OnDisable()
    {
        UnsubscribeFromAllCars();

        StopCarAudio();
        StopEffectAudio();
    }

    private void OnEnable()
    {
        if (isInitialized)
            SubscribeToAllCars();
    }

    public void InitializeAudioSystem()
    {
        if (isInitialized)
        {
            Debug.Log(
                "[ArcadeRaceAudioManager] O sistema já foi inicializado.",
                this);
            return;
        }

        DetectHumanPlayerCars();

        CreateCarAudioSources();
        CreateEffectVoices();

        SubscribeToAllCars();

        isInitialized = true;

        Debug.Log(
            $"[ArcadeRaceAudioManager] Inicializado para " +
            $"{players.Length} jogador(es) humano(s).",
            this);
    }


    private void DetectHumanPlayerCars()
    {
        var detectedPlayers = new List<CarAudioSetup>();

        SCR_RayBasedCarPhysics[] allCars =
            FindObjectsByType<SCR_RayBasedCarPhysics>(
                FindObjectsSortMode.None);

        foreach (SCR_RayBasedCarPhysics car in allCars)
        {
            if (car == null)
                continue;

            RacerStatus status = car.GetComponent<RacerStatus>();

            if (status == null || !status.isPlayer)
                continue;

            CarAudioClips selectedProfile = GetProfileForCar(car);

            if (selectedProfile == null)
            {
                Debug.LogWarning(
                    $"[ArcadeRaceAudioManager] Não há perfil de áudio " +
                    $"para o carro {car.name}.",
                    car);
                continue;
            }

            detectedPlayers.Add(new CarAudioSetup
            {
                car = car,
                activeClips = selectedProfile
            });

            if (detectedPlayers.Count >= MAX_HUMAN_PLAYERS)
                break;
        }

        players = detectedPlayers.ToArray();

        if (players.Length == 0)
        {
            Debug.LogWarning(
                "[ArcadeRaceAudioManager] Nenhum carro humano foi " +
                "encontrado. Verifique RacerStatus.isPlayer e a ordem " +
                "dos setups no GameManager.",
                this);
        }
    }

    private CarAudioClips GetProfileForCar(
        SCR_RayBasedCarPhysics car)
    {
        switch (car.carType)
        {
            case CarType.hover:
                return hoverClips;

            case CarType.classic:
                return classicClips;

            default:
                Debug.LogWarning(
                    $"[ArcadeRaceAudioManager] Tipo de carro não " +
                    $"reconhecido em {car.name}.",
                    car);
                return null;
        }
    }

    private void CreateCarAudioSources()
    {
        int count = players.Length;

        engineSources = new AudioSource[count];
        driftSources = new AudioSource[count];
        targetEngineVolumes = new float[count];

        for (int i = 0; i < count; i++)
        {
            engineSources[i] = CreateSource(
                $"Engine_P{i + 1}", true);

            driftSources[i] = CreateSource(
                $"Drift_P{i + 1}", true);
        }
    }

    private void CreateEffectVoices()
    {
        int count = Mathf.Max(1, effectVoiceCount);

        effectVoices = new EffectVoice[count];

        for (int i = 0; i < count; i++)
        {
            effectVoices[i] = new EffectVoice
            {
                source = CreateSource($"SFX_{i + 1}", false),
                priority = 0
            };
        }
    }

    private AudioSource CreateSource(string sourceName, bool loop)
    {
        GameObject sourceObject = new GameObject(sourceName);
        sourceObject.transform.SetParent(transform, false);

        AudioSource source = sourceObject.AddComponent<AudioSource>();

        source.playOnAwake = false;
        source.loop = loop;

        // Mixagem arcade compartilhada: áudio não posicional.
        source.spatialBlend = 0f;
        source.dopplerLevel = 0f;
        source.priority = 128;

        return source;
    }

    private void SubscribeToAllCars()
    {
        if (eventsSubscribed)
            return;

        int count = players.Length;

        gearHandlers = new Action[count];
        turboHandlers = new Action[count];
        collisionHandlers = new Action<float, Vector3>[count];
        landingHandlers = new Action<float>[count];

        for (int i = 0; i < count; i++)
            SubscribeToCar(i);

        eventsSubscribed = true;
    }

    private void SubscribeToCar(int index)
    {
        CarAudioSetup setup = players[index];

        if (setup == null ||
            setup.car == null ||
            setup.activeClips == null)
            return;

        SCR_RayBasedCarPhysics car = setup.car;
        CarAudioClips clips = setup.activeClips;

        gearHandlers[index] =
            () => PlayEffect(clips.gearShift, PRIORITY_GEAR);

        turboHandlers[index] =
            () => PlayEffect(clips.turboStart, PRIORITY_TURBO);

        collisionHandlers[index] =
            (force, direction) => HandleCollision(setup, force);

        landingHandlers[index] =
            airTime => PlayEffect(clips.landing, PRIORITY_DRIFT);

        car.OnUpGear += gearHandlers[index];
        car.OnTurboStart += turboHandlers[index];
        car.OnCollision += collisionHandlers[index];
        car.OnLand += landingHandlers[index];
    }

    private void UnsubscribeFromAllCars()
    {
        if (!eventsSubscribed)
            return;

        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] == null || players[i].car == null)
                continue;

            SCR_RayBasedCarPhysics car = players[i].car;

            if (gearHandlers != null && gearHandlers[i] != null)
                car.OnUpGear -= gearHandlers[i];

            if (turboHandlers != null && turboHandlers[i] != null)
                car.OnTurboStart -= turboHandlers[i];

            if (collisionHandlers != null &&
                collisionHandlers[i] != null)
                car.OnCollision -= collisionHandlers[i];

            if (landingHandlers != null && landingHandlers[i] != null)
                car.OnLand -= landingHandlers[i];
        }

        eventsSubscribed = false;
    }


    private void HandleCollision(CarAudioSetup setup, float force)
    {
        if (setup == null || setup.activeClips == null)
            return;

        CarAudioClips clips = setup.activeClips;
        bool heavy = force >= collisionHeavyThreshold;

        if(force < 0.3f) return;

        AudioClip clip = heavy
            ? (clips.heavyCollision != null
                ? clips.heavyCollision
                : clips.collision)
            : clips.collision;

        PlayEffect(
            clip,
            heavy ? PRIORITY_HEAVY_COLLISION : PRIORITY_COLLISION,
            heavy ? 1f : 0.75f);
    }


    private void Update()
    {
        if (!isInitialized)
            return;

        UpdateDominantEngine();
        UpdateEngineSources();
        UpdateDriftSources();
        CleanupEffectVoices();
    }


    private void UpdateDominantEngine()
    {
        if (players.Length < 2 ||
            players[0]?.car == null ||
            players[1]?.car == null)
        {
            dominantPlayer = 0;
            return;
        }

        float score0 = GetEngineScore(players[0].car);
        float score1 = GetEngineScore(players[1].car);

        float difference = score0 - score1;

        // Histerese para evitar trocas frenéticas de dominância.
        if (dominantPlayer == 0 &&
            difference < -dominantSwitchThreshold)
        {
            dominantPlayer = 1;
        }
        else if (dominantPlayer == 1 &&
                 difference > dominantSwitchThreshold)
        {
            dominantPlayer = 0;
        }
    }

    private float GetEngineScore(SCR_RayBasedCarPhysics car)
    {
        float rpm = Mathf.InverseLerp(
            3000f, 9000f, car.engineRPM);

        float speed = Mathf.Clamp01(car.speedKMH / 400f);

        float score = rpm * 0.65f + speed * 0.35f;

        if (car.IsTurboActive())
            score += 0.2f;

        return score;
    }

   

    private void UpdateEngineSources()
    {
        for (int i = 0; i < players.Length; i++)
        {
            CarAudioSetup setup = players[i];

            if (setup == null ||
                i >= engineSources.Length)
                continue;

            SCR_RayBasedCarPhysics car = setup.car;
            CarAudioClips clips = setup.activeClips;
            AudioSource source = engineSources[i];

            if (source == null)
                continue;

            if (car == null ||
                clips == null ||
                clips.engineLoop == null)
            {
                source.Stop();
                source.volume = 0f;
                continue;
            }

            if (source.clip != clips.engineLoop)
            {
                source.clip = clips.engineLoop;
                source.loop = true;
                source.Play();
            }
            else if (!source.isPlaying)
            {
                source.Play();
            }

            float rpm = Mathf.InverseLerp(
                3000f, 9000f, car.engineRPM);

            float targetPitch = Mathf.Lerp(
                clips.minPitch,
                clips.maxPitch,
                rpm);

            source.pitch = Mathf.Lerp(
                source.pitch,
                targetPitch,
                Time.deltaTime * pitchTransitionSpeed);

            float dominance = i == dominantPlayer
                ? 1f
                : secondaryEngineVolume;

            targetEngineVolumes[i] =
                clips.engineVolume * dominance;

            source.volume = Mathf.Lerp(
                source.volume,
                targetEngineVolumes[i],
                Time.deltaTime * volumeTransitionSpeed);
        }
    }


    private void UpdateDriftSources()
    {
        for (int i = 0; i < players.Length; i++)
        {
            CarAudioSetup setup = players[i];

            if (setup == null ||
                i >= driftSources.Length)
                continue;

            SCR_RayBasedCarPhysics car = setup.car;
            CarAudioClips clips = setup.activeClips;
            AudioSource source = driftSources[i];

            if (source == null)
                continue;

            if (car == null ||
                clips == null ||
                clips.driftLoop == null ||
                !car.IsDrifting())
            {
                source.volume = Mathf.MoveTowards(
                    source.volume,
                    0f,
                    Time.deltaTime * volumeTransitionSpeed);

                if (source.volume <= 0.01f)
                    source.Stop();

                continue;
            }

            if (source.clip != clips.driftLoop)
            {
                source.clip = clips.driftLoop;
                source.loop = true;
                source.Play();
            }
            else if (!source.isPlaying)
            {
                source.Play();
            }

            float speedFactor = Mathf.Clamp01(
                car.speedKMH / 200f);

            float dominance = i == dominantPlayer
                ? 1f
                : secondaryEngineVolume;

            float targetVolume =
                clips.driftVolume * speedFactor * dominance;

            source.volume = Mathf.Lerp(
                source.volume,
                targetVolume,
                Time.deltaTime * volumeTransitionSpeed);
        }
    }


    private void PlayEffect(
        AudioClip clip,
        int priority,
        float volume = 1f)
    {
        if (!isActiveAndEnabled ||
            clip == null ||
            effectVoices == null)
            return;

        EffectVoice selected = null;

        // Primeiro: procura uma voz livre.
        foreach (EffectVoice voice in effectVoices)
        {
            if (voice == null || voice.source == null)
                continue;

            if (!voice.source.isPlaying)
            {
                selected = voice;
                break;
            }
        }

        // Sem vozes livres: substitui apenas uma prioridade menor.
        if (selected == null)
        {
            foreach (EffectVoice voice in effectVoices)
            {
                if (voice == null ||
                    voice.source == null ||
                    voice.priority >= priority)
                    continue;

                if (selected == null ||
                    voice.priority < selected.priority)
                {
                    selected = voice;
                }
            }
        }

        // Não interrompe um efeito de prioridade igual ou superior.
        if (selected == null)
            return;

        selected.source.Stop();
        selected.source.clip = clip;
        selected.source.loop = false;
        selected.source.volume = Mathf.Clamp01(
            volume * effectsMasterVolume);

        selected.priority = priority;
        selected.source.Play();
    }

    private void CleanupEffectVoices()
    {
        if (effectVoices == null)
            return;

        foreach (EffectVoice voice in effectVoices)
        {
            if (voice != null &&
                voice.source != null &&
                !voice.source.isPlaying)
            {
                voice.priority = 0;
            }
        }
    }

    private void StopCarAudio()
    {
        StopSources(engineSources);
        StopSources(driftSources);
    }

    private void StopEffectAudio()
    {
        if (effectVoices == null)
            return;

        foreach (EffectVoice voice in effectVoices)
        {
            if (voice == null || voice.source == null)
                continue;

            voice.source.Stop();
            voice.priority = 0;
        }
    }

    private void StopSources(AudioSource[] sources)
    {
        if (sources == null)
            return;

        foreach (AudioSource source in sources)
        {
            if (source == null)
                continue;

            source.Stop();
            source.volume = 0f;
        }
    }
}
