using UnityEngine;

public class SCR_CarAudioSystem : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SCR_RayBasedCarPhysics carPhysics;
    [SerializeField] private Rigidbody rb;

    [Header("Audio Sources")]
    [SerializeField] private AudioSource engineSource;
    [SerializeField] private AudioSource skidSource;
    [SerializeField] private AudioSource turboSource;
    [SerializeField] private AudioSource impactSource;

    [Header("Engine")]
    [SerializeField] private AudioClip engineClip;

    [SerializeField] private float minPitch = 0.75f;
    [SerializeField] private float maxPitch = 1.65f;

    [SerializeField] private float minVolume = 0.2f;
    [SerializeField] private float maxVolume = 0.85f;

    [SerializeField] private float pitchSmooth = 6f;
    [SerializeField] private float volumeSmooth = 4f;

    [Header("Skid")]
    [SerializeField] private AudioClip skidClip;

    [SerializeField] private float skidStartSlip = 0.25f;
    [SerializeField] private float skidMaxSlip = 1.5f;

    [SerializeField] private float skidMinPitch = 0.85f;
    [SerializeField] private float skidMaxPitch = 1.25f;

    [SerializeField] private float skidMaxVolume = 0.8f;

    [SerializeField] private float skidSmooth = 8f;

    [Header("Turbo / Nitro")]
    [SerializeField] private AudioClip turboClip;

    [SerializeField] private float turboVolume = 0.35f;
    [SerializeField] private float turboPitch = 1.1f;

    [SerializeField] private float turboFadeSpeed = 10f;

    [Header("Impact")]
    [SerializeField] private AudioClip[] impactClips;

    [SerializeField] private float minImpactForce = 8f;
    [SerializeField] private float maxImpactForce = 40f;

    [SerializeField] private float collisionCooldown = 0.08f;

    [Header("Spatial")]
    [SerializeField] private float spatialBlend = 1f;
    [SerializeField] private float minDistance = 8f;
    [SerializeField] private float maxDistance = 80f;

    private float turboTarget;
    private float currentTurboVolume;

    private float currentSkidVolume;

    private float lastCollisionTime;

    private void Awake()
    {
        if (carPhysics == null)
            carPhysics = GetComponent<SCR_RayBasedCarPhysics>();

        if (rb == null)
            rb = GetComponent<Rigidbody>();

        SetupSources();
    }

    private void Start()
    {
        StartEngineLoop();

        if (carPhysics != null)
        {
            carPhysics.OnTurboStart += OnTurboStart;
            carPhysics.OnTurboEnd += OnTurboEnd;
            carPhysics.OnCollision += OnCollision;
        }
    }

    private void Update()
    {
        UpdateEngine();
        UpdateSkid();
        UpdateTurbo();
    }

    #region ENGINE

    private void UpdateEngine()
    {
        if (engineSource == null || carPhysics == null)
            return;

        float rpmNormalized = Mathf.Clamp01(carPhysics.engineRPM / 9000f);

        float speedNormalized =
            Mathf.Clamp01(
                carPhysics.GetCurrentSpeed() /
                Mathf.Max(1f, carPhysics.OriginalMaxSpeed())
            );

        // RPM domina o pitch
        float targetPitch =
            Mathf.Lerp(minPitch, maxPitch, rpmNormalized);

        // velocidade ajuda levemente
        targetPitch += speedNormalized * 0.08f;

        // throttle dá vida
        targetPitch += Mathf.Abs(carPhysics.GetThrottleInput()) * 0.05f;

        float targetVolume =
            Mathf.Lerp(minVolume, maxVolume, rpmNormalized);

        // reduz um pouco motor durante drift forte
        float driftAmount = GetLateralSlip();
        targetVolume *= Mathf.Lerp(1f, 0.8f, driftAmount);

        engineSource.pitch =
            Mathf.Lerp(
                engineSource.pitch,
                targetPitch,
                Time.deltaTime * pitchSmooth
            );

        engineSource.volume =
            Mathf.Lerp(
                engineSource.volume,
                targetVolume,
                Time.deltaTime * volumeSmooth
            );
    }

    #endregion

    #region SKID

    private void UpdateSkid()
    {
        if (skidSource == null || carPhysics == null)
            return;

        float speed =
            Mathf.Abs(carPhysics.GetCurrentSpeed());

        // sem skid em baixa velocidade
        if (speed < 20f)
        {
            FadeOutSkid();
            return;
        }

        float slip = GetLateralSlip();

        float skidAmount =
            Mathf.InverseLerp(
                skidStartSlip,
                skidMaxSlip,
                slip
            );

        // só toca se realmente estiver escorregando
        if (skidAmount <= 0.01f)
        {
            FadeOutSkid();
            return;
        }

        if (!skidSource.isPlaying)
            skidSource.Play();

        float targetVolume =
            skidAmount * skidMaxVolume;

        float targetPitch =
            Mathf.Lerp(
                skidMinPitch,
                skidMaxPitch,
                skidAmount
            );

        currentSkidVolume =
            Mathf.Lerp(
                currentSkidVolume,
                targetVolume,
                Time.deltaTime * skidSmooth
            );

        skidSource.volume = currentSkidVolume;

        skidSource.pitch =
            Mathf.Lerp(
                skidSource.pitch,
                targetPitch,
                Time.deltaTime * skidSmooth
            );
    }

    private void FadeOutSkid()
    {
        currentSkidVolume =
            Mathf.Lerp(
                currentSkidVolume,
                0f,
                Time.deltaTime * skidSmooth
            );

        skidSource.volume = currentSkidVolume;

        if (currentSkidVolume <= 0.01f && skidSource.isPlaying)
            skidSource.Stop();
    }

    // baseado em velocidade lateral REAL
    private float GetLateralSlip()
    {
        Vector3 localVelocity =
            transform.InverseTransformDirection(rb.linearVelocity);

        float lateralVelocity =
            Mathf.Abs(localVelocity.x);

        return lateralVelocity / 12f;
    }

    #endregion

    #region TURBO

    private void UpdateTurbo()
    {
        if (turboSource == null)
            return;

        currentTurboVolume =
            Mathf.Lerp(
                currentTurboVolume,
                turboTarget,
                Time.deltaTime * turboFadeSpeed
            );

        turboSource.volume = currentTurboVolume;

        if (currentTurboVolume > 0.01f)
        {
            if (!turboSource.isPlaying)
                turboSource.Play();
        }
        else
        {
            if (turboSource.isPlaying)
                turboSource.Stop();
        }
    }

    private void OnTurboStart()
    {
        turboTarget = turboVolume;

        turboSource.pitch =
            turboPitch +
            Random.Range(-0.05f, 0.05f);
    }

    private void OnTurboEnd()
    {
        turboTarget = 0f;
    }

    #endregion

    #region IMPACT

    private void OnCollision(float intensity, Vector3 direction)
    {
        if (impactSource == null)
            return;

        if (Time.time - lastCollisionTime < collisionCooldown)
            return;

        lastCollisionTime = Time.time;

        if (impactClips == null || impactClips.Length == 0)
            return;

        float volume =
            Mathf.InverseLerp(
                minImpactForce,
                maxImpactForce,
                intensity
            );

        if (volume <= 0.05f)
            return;

        AudioClip clip =
            impactClips[
                Random.Range(0, impactClips.Length)
            ];

        impactSource.pitch =
            Random.Range(0.92f, 1.08f);

        impactSource.PlayOneShot(
            clip,
            Mathf.Clamp01(volume)
        );
    }

    #endregion

    #region SETUP

    private void SetupSources()
    {
        engineSource = CreateSource("Engine", true);
        skidSource = CreateSource("Skid", true);
        turboSource = CreateSource("Turbo", true);
        impactSource = CreateSource("Impact", false);

        engineSource.clip = engineClip;
        skidSource.clip = skidClip;
        turboSource.clip = turboClip;
    }

    private AudioSource CreateSource(string sourceName, bool loop)
    {
        Transform child = transform.Find(sourceName);

        AudioSource source;

        if (child != null)
        {
            source = child.GetComponent<AudioSource>();
        }
        else
        {
            GameObject go = new GameObject(sourceName);

            go.transform.SetParent(transform);
            go.transform.localPosition = Vector3.zero;

            source = go.AddComponent<AudioSource>();
        }

        source.loop = loop;
        source.playOnAwake = false;

        source.spatialBlend = spatialBlend;
        source.minDistance = minDistance;
        source.maxDistance = maxDistance;

        source.rolloffMode = AudioRolloffMode.Logarithmic;

        return source;
    }

    private void StartEngineLoop()
    {
        if (engineSource == null || engineClip == null)
            return;

        engineSource.clip = engineClip;
        engineSource.volume = 0f;
        engineSource.pitch = minPitch;

        engineSource.Play();
    }

    #endregion

    private void OnDestroy()
    {
        if (carPhysics != null)
        {
            carPhysics.OnTurboStart -= OnTurboStart;
            carPhysics.OnTurboEnd -= OnTurboEnd;
            carPhysics.OnCollision -= OnCollision;
        }
    }
}