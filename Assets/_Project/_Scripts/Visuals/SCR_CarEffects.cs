using UnityEngine;

public class SCR_CarEffects : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SCR_RayBasedCarPhysics carPhysics;
    
    [Header("Drift VFX")]
    [SerializeField] private ParticleSystem[] driftParticles; 
    [SerializeField] private float minEmissionRate = 10f;
    [SerializeField] private float maxEmissionRate = 100f;
    [SerializeField] private float minStartSize = 5f; // Novo: Tamanho mínimo
    [SerializeField] private float maxStartSize = 30f; // Novo: Tamanho máximo
    
    [Header("Drift Boost VFX")]
    [SerializeField] private ParticleSystem boostParticles;
    
    [Header("Turbo VFX")]
    [SerializeField] private ParticleSystem turboParticles; // Novo: Sistema de Turbo

    private ParticleSystem.EmissionModule[] driftEmissionModules;
    private ParticleSystem.MainModule[] driftMainModules; // Cache para o tamanho
    private bool _lastBoostState = false;
    private bool _lastTurboState = false;

    Rigidbody rb;

    void Start()
    {
        if (carPhysics == null) carPhysics = GetComponentInParent<SCR_RayBasedCarPhysics>();

        // Inicializa caches do Drift
        driftEmissionModules = new ParticleSystem.EmissionModule[driftParticles.Length];
        driftMainModules = new ParticleSystem.MainModule[driftParticles.Length];

        for (int i = 0; i < driftParticles.Length; i++)
        {
            driftEmissionModules[i] = driftParticles[i].emission;
            driftMainModules[i] = driftParticles[i].main;
            
            // Garante que comece zerado
            driftEmissionModules[i].rateOverTime = 0;
        }

        // Inicializa Boost e Turbo
        SetupToggleableParticles(boostParticles);
        SetupToggleableParticles(turboParticles);

        rb = carPhysics.GetComponent<Rigidbody>();
    }

    private void SetupToggleableParticles(ParticleSystem ps)
    {
        if (ps != null)
        {
            var em = ps.emission;
            em.enabled = false;
        }
    }

    void Update()
    {
        HandleDriftVFX();
        HandleBoostVFX();
        HandleTurboVFX(); // Nova chamada
    }

    private void HandleDriftVFX()
    {
        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);
        float sideSlip = Mathf.Abs(localVelocity.x);
        float speedRatio = rb.linearVelocity.magnitude / carPhysics.OriginalMaxSpeed();

        bool isSlipping = sideSlip > 5f && speedRatio > 0.15f; 
        bool isIntentionallyDrifting = carPhysics.IsDrifting();
        bool showEffects = (isSlipping || isIntentionallyDrifting) && carPhysics.IsGrounded;

        for (int i = 0; i < driftParticles.Length; i++)
        {
            if (showEffects)
            {
                // Calcula intensidade (0 a 1)
                float intensity = isIntentionallyDrifting ? 1.0f : Mathf.Clamp01(sideSlip / 15f);
                
                // Aplica emissão
                driftEmissionModules[i].rateOverTime = Mathf.Lerp(minEmissionRate, maxEmissionRate, intensity);
                
                // Aplica variação de tamanho (Start Size)
                driftMainModules[i].startSize = Mathf.Lerp(minStartSize, maxStartSize, intensity);
            }
            else
            {
                driftEmissionModules[i].rateOverTime = 0;
            }
        }
    }

    private void HandleBoostVFX()
    {
        UpdateParticleState(boostParticles, carPhysics.IsDriftBoostActive(), ref _lastBoostState);
    }

    private void HandleTurboVFX()
    {
        // Utiliza a bool IsTurboActive() conforme solicitado
        UpdateParticleState(turboParticles, carPhysics.IsTurboActive(), ref _lastTurboState);
    }

    // Método auxiliar para evitar repetição de código entre Boost e Turbo
    private void UpdateParticleState(ParticleSystem ps, bool currentState, ref bool lastState)
    {
        if (ps == null) return;

        if (currentState && !lastState)
        {
            ps.Play();
            var em = ps.emission;
            em.enabled = true;
        }
        else if (!currentState && lastState)
        {
            var em = ps.emission;
            em.enabled = false;
            ps.Stop();
        }
        lastState = currentState;
    }
}