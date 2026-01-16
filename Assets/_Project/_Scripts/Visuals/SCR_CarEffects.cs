using UnityEngine;

public class SCR_CarEffects : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SCR_RayBasedCarPhysics carPhysics;
    
    [Header("Drift VFX")]
    [SerializeField] private ParticleSystem[] driftParticles; // Lista para suportar fumaça em vários pneus
    [SerializeField] private float minEmissionRate = 10f;
    [SerializeField] private float maxEmissionRate = 100f;
    
    [Header("Drift Boost VFX")]
    [SerializeField] private ParticleSystem boostParticles;
    
    private ParticleSystem.EmissionModule[] emissionModules;
    private bool _lastBoostState = false;

    Rigidbody rb;

    void Start()
    {
        if (carPhysics == null) carPhysics = GetComponentInParent<SCR_RayBasedCarPhysics>();

        // Inicializa os módulos de emissão para evitar chamadas de cache no Update
        emissionModules = new ParticleSystem.EmissionModule[driftParticles.Length];
        for (int i = 0; i < driftParticles.Length; i++)
        {
            emissionModules[i] = driftParticles[i].emission;
            emissionModules[i].enabled = true; // Começa desligado

            // Começa com zero para não emitir ao dar Play
            var rate = emissionModules[i].rateOverTime;
            rate.constant = 0;
            emissionModules[i].rateOverTime = rate;
        }

        if (boostParticles != null)
        {
            var boostEmission = boostParticles.emission;
            boostEmission.enabled = false;
        }

        rb = carPhysics.GetComponent<Rigidbody>();
    }

    void Update()
    {
        HandleDriftVFX();
        HandleBoostVFX();
    }

    private void HandleDriftVFX()
    {
        // Calcula a velocidade lateral (quanto o carro está escorregando para os lados)
        // TransformVector transforma a velocidade global em local. O eixo X é o lado.
        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);
        float sideSlip = Mathf.Abs(localVelocity.x);
        
        float speedRatio = rb.linearVelocity.magnitude / carPhysics.OriginalMaxSpeed();

        // --- LÓGICA UNIVERSAL ---
        // Ativamos se: 
        // 1. O carro estiver escorregando lateralmente acima de um limite (ex: 5m/s)
        // 2. OU se o sistema de drift estiver explicitamente ativo
        bool isSlipping = sideSlip > 5f && speedRatio > 0.15f; 
        bool isIntentionallyDrifting = carPhysics.IsDrifting();

        bool showEffects = isSlipping || isIntentionallyDrifting;

        for (int i = 0; i < driftParticles.Length; i++)
        {
            var em = emissionModules[i];
            if (showEffects)
            {
                // A intensidade aumenta conforme o escorregão lateral aumenta
                float intensity = isIntentionallyDrifting ? 1.0f : Mathf.Clamp01(sideSlip / 15f);
                em.rateOverTime = Mathf.Lerp(minEmissionRate, maxEmissionRate, intensity);
            }
            else
            {
                em.rateOverTime = 0;
            }
        }
    }

    private void HandleBoostVFX()
    {
        if (boostParticles == null) return;

        bool isBoosting = carPhysics.IsDriftBoostActive();

        // Dispara o boost apenas quando o estado muda para verdadeiro
        if (isBoosting && !_lastBoostState)
        {
            boostParticles.Play();
            var em = boostParticles.emission;
            em.enabled = true;
        }
        else if (!isBoosting && _lastBoostState)
        {
            var em = boostParticles.emission;
            em.enabled = false;
            boostParticles.Stop();
        }

        _lastBoostState = isBoosting;
    }
}