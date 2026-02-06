using System.Collections.Generic;
using UnityEngine;

public class SCR_CarEffects : MonoBehaviour
{
    [System.Serializable]
    private struct ParticleSnapshot
    {
        public bool emissionEnabled;
        public float rateOverTime;

        public float startLifetime;
        public float startSpeed;
        public float startSize;
    }


    [Header("References")]
    [SerializeField] private SCR_RayBasedCarPhysics carPhysics;

    private List<ParticleSystem> allParticles = new();
    private bool visualsWereDisabled = false;
    
    [Header("Classic Drift VFX")]
    [SerializeField] private ParticleSystem[] classicDriftParticles; 
    [SerializeField] private float minEmissionRate = 10f;
    [SerializeField] private float maxEmissionRate = 100f;
    [SerializeField] private float minStartSize = 5f; 
    [SerializeField] private float maxStartSize = 30f; 

    [Header("Hover Drift VFX")]
    [SerializeField] public ParticleSystem[] hoverDriftParticles; // Nova array para modo Hover
    
    [Header("Drift Boost VFX")]
    [SerializeField] private ParticleSystem boostParticles;
    
    [Header("Turbo VFX")]
    [SerializeField] private ParticleSystem turboParticles;

    [Header("Spark VFX (Collisions)")]
    [SerializeField] private ParticleSystem sparksLeft;
    [SerializeField] private ParticleSystem sparksRight;
    [SerializeField] private float minSparkEmission = 10f;
    [SerializeField] private float maxSparkEmission = 30f;
    [SerializeField] private float relativeSpeedThreshold = 10f;

    [Header("Collision Impact")]
    [SerializeField] SCR_ImpactEffect[] impactEffects;

    [Header("Collision Cooldown")]
    [SerializeField] private float collisionCooldown = 0.5f;
    private float lastCollisionTime = -999f;


    [Header("Heat distortion particles")]
    [SerializeField] private ParticleSystem HeatDistortionParticles;
    private ParticleSnapshot heatSnapshot;
    private bool heatSnapshotCached = false;


    // Módulos Classic
    private ParticleSystem.EmissionModule[] classicEmissionModules;
    private ParticleSystem.MainModule[] classicMainModules;
    
    // Módulos Hover
    private ParticleSystem.EmissionModule[] hoverEmissionModules;

    private ParticleSystem.EmissionModule leftSparkEM;
    private ParticleSystem.EmissionModule rightSparkEM;
    private bool isCollidingLeft, isCollidingRight;
    private bool _lastBoostState = false;
    private bool _lastTurboState = false;

    Rigidbody rb;

    void Start()
    {
        CacheAllParticles();
        if (HeatDistortionParticles != null)
        {
            var em = HeatDistortionParticles.emission;
            var main = HeatDistortionParticles.main;

            heatSnapshot = new ParticleSnapshot
            {
                emissionEnabled = em.enabled,
                rateOverTime = em.rateOverTime.constant,

                startLifetime = main.startLifetime.constant,
                startSpeed = main.startSpeed.constant,
                startSize = main.startSize.constant
            };

            heatSnapshotCached = true;
        }

        if (carPhysics == null) carPhysics = GetComponentInParent<SCR_RayBasedCarPhysics>();

        // Cache Classic
        classicEmissionModules = new ParticleSystem.EmissionModule[classicDriftParticles.Length];
        classicMainModules = new ParticleSystem.MainModule[classicDriftParticles.Length];
        for (int i = 0; i < classicDriftParticles.Length; i++)
        {
            classicEmissionModules[i] = classicDriftParticles[i].emission;
            classicMainModules[i] = classicDriftParticles[i].main;
            classicEmissionModules[i].rateOverTime = 0;
        }

        // Cache Hover
        hoverEmissionModules = new ParticleSystem.EmissionModule[hoverDriftParticles.Length];
        for (int i = 0; i < hoverDriftParticles.Length; i++)
        {
            hoverEmissionModules[i] = hoverDriftParticles[i].emission;
            hoverEmissionModules[i].rateOverTime = 0;
        }
        

        SetupToggleableParticles(boostParticles);
        SetupToggleableParticles(turboParticles);

        if (sparksLeft) { leftSparkEM = sparksLeft.emission; leftSparkEM.rateOverTime = 0; }
        if (sparksRight) { rightSparkEM = sparksRight.emission; rightSparkEM.rateOverTime = 0; }

        rb = carPhysics.GetComponent<Rigidbody>();
    }

    void Update()
    {
       if (!carPhysics.CanUpdateVisuals)
        {
            DisableAllParticles();
            return;
        }

        RestoreAllParticles();

        HandleDriftVFX();
        HandleBoostVFX();
        HandleTurboVFX();
        ResetSparkStates();
    }

    private void CacheAllParticles()
    {
        void Add(ParticleSystem ps)
        {
            if (ps != null && !allParticles.Contains(ps))
                allParticles.Add(ps);
        }

        // Classic
        foreach (var ps in classicDriftParticles)
            Add(ps);

        // Hover
        foreach (var ps in hoverDriftParticles)
            Add(ps);

        // Toggleables
        Add(boostParticles);
        Add(turboParticles);

        // Sparks
        Add(sparksLeft);
        Add(sparksRight);

        // Heat distortion
        Add(HeatDistortionParticles);
    }

    private void DisableAllParticles()
    {
        if (visualsWereDisabled) return;

        foreach (var ps in allParticles)
        {
            if (ps == null) continue;

            var em = ps.emission;
            em.rateOverTime = 0;          // reversível
            em.enabled = true;            // nunca desligar

            if (ps.isPlaying)
                ps.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }

        visualsWereDisabled = true;

        _lastBoostState = false;
        _lastTurboState = false;

        isCollidingLeft = false;
        isCollidingRight = false;
    }

    private void RestoreAllParticles()
    {
        if (!visualsWereDisabled) return;

        foreach (var ps in allParticles)
        {
            if (ps == null) continue;

            var em = ps.emission;
            em.enabled = true;   // garante estado válido

            if (!ps.isPlaying)
                ps.Play();
        }

        if (HeatDistortionParticles != null && heatSnapshotCached)
        {
            var em = HeatDistortionParticles.emission;
            var main = HeatDistortionParticles.main;

            em.enabled = heatSnapshot.emissionEnabled;
            em.rateOverTime = heatSnapshot.rateOverTime;

            main.startLifetime = heatSnapshot.startLifetime;
            main.startSpeed = heatSnapshot.startSpeed;
            main.startSize = heatSnapshot.startSize;

            if (!HeatDistortionParticles.isPlaying)
                HeatDistortionParticles.Play();
        }

        visualsWereDisabled = false;
    }



    private void HandleDriftVFX()
    {
        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);
        float sideSlip = Mathf.Abs(localVelocity.x);
        float speedRatio = rb.linearVelocity.magnitude / carPhysics.OriginalMaxSpeed();

        bool isSlipping = sideSlip > 5f && speedRatio > 0.15f; 
        bool isIntentionallyDrifting = carPhysics.IsDrifting();
        bool showEffects = (isSlipping || isIntentionallyDrifting) && carPhysics.IsGrounded;

        // Checa o tipo de carro no script de física
        bool isHoverMode = carPhysics.carType == CarType.hover;

        // 1. Lógica para Partículas Clássicas
        for (int i = 0; i < classicDriftParticles.Length; i++)
        {
            if (showEffects && !isHoverMode)
            {
                float intensity = isIntentionallyDrifting ? 1.0f : Mathf.Clamp01(sideSlip / 15f);
                classicEmissionModules[i].rateOverTime = Mathf.Lerp(minEmissionRate, maxEmissionRate, intensity);
                classicMainModules[i].startSize = Mathf.Lerp(minStartSize, maxStartSize, intensity);
            }
            else
            {
                classicEmissionModules[i].rateOverTime = 0;
            }
        }

        // 2. Lógica para Partículas Hover (Sem alteração de tamanho)
        for (int i = 0; i < hoverDriftParticles.Length; i++)
        {
            if (showEffects && isHoverMode)
            {
                float intensity = isIntentionallyDrifting ? 1.0f : Mathf.Clamp01(sideSlip / 15f);
                hoverEmissionModules[i].rateOverTime = Mathf.Lerp(minEmissionRate, maxEmissionRate, intensity);
                // O startSize aqui permanece o original do prefab
            }
            else
            {
                hoverEmissionModules[i].rateOverTime = 0;
            }
        }
    }

    private void SetupToggleableParticles(ParticleSystem ps)
    {
        if (ps != null)
        {
            var em = ps.emission;
            em.enabled = false;
        }
    }

    private void HandleBoostVFX()
    {
        UpdateParticleState(boostParticles, carPhysics.IsDriftBoostActive(), ref _lastBoostState);
    }

    private void HandleTurboVFX()
    {
        UpdateParticleState(turboParticles, carPhysics.IsTurboActive(), ref _lastTurboState);
    }

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

    private void OnCollisionStay(Collision collision)
    {
        int layer = collision.gameObject.layer;
        bool isWall = layer == LayerMask.NameToLayer("Walls");
        bool isCar = layer == LayerMask.NameToLayer("Car");

        if (!isWall && !isCar) return;

        float mySpeed = transform.InverseTransformDirection(rb.linearVelocity).z;
        float otherSpeed = 0;

        if (isCar)
        {
            var otherPhysics = collision.gameObject.GetComponentInParent<SCR_RayBasedCarPhysics>();
            if (otherPhysics != null)
            {
                Rigidbody otherRB = otherPhysics.GetComponent<Rigidbody>();
                otherSpeed = transform.InverseTransformDirection(otherRB.linearVelocity).z;
            }
        }

        float scrapIntensity = Mathf.Abs(mySpeed - otherSpeed);
        if (scrapIntensity < relativeSpeedThreshold) return;

        foreach (ContactPoint contact in collision.contacts)
        {
            Vector3 localContactPoint = transform.InverseTransformPoint(contact.point);
            float emissionRate = Mathf.Lerp(minSparkEmission, maxSparkEmission, scrapIntensity / 100f);

            if (localContactPoint.x > 0.1f)
            {
                isCollidingRight = true;
                rightSparkEM.rateOverTime = emissionRate;
                if (!sparksRight.isPlaying) sparksRight.Play();
            }
            else if (localContactPoint.x < -0.1f)
            {
                isCollidingLeft = true;
                leftSparkEM.rateOverTime = emissionRate;
                if (!sparksLeft.isPlaying) sparksLeft.Play();
            }
        }
    }

    private void ResetSparkStates()
    {
        if (!isCollidingLeft && sparksLeft.isPlaying) 
        {
            leftSparkEM.rateOverTime = 0;
            sparksLeft.Stop();
        }
        if (!isCollidingRight && sparksRight.isPlaying) 
        {
            rightSparkEM.rateOverTime = 0;
            sparksRight.Stop();
        }
        isCollidingLeft = false;
        isCollidingRight = false;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (Time.time < lastCollisionTime + collisionCooldown)
            return; //cool down

        // Verifica se a colisão é relevante (parede ou carro)
        int layer = collision.gameObject.layer;
        if (layer != LayerMask.NameToLayer("Walls") && layer != LayerMask.NameToLayer("Car")) return;

        // Só toca o impacto se a força for considerável
        if (collision.relativeVelocity.magnitude < relativeSpeedThreshold) return;

        // Pega o ponto de contato para posicionar o efeito
        ContactPoint contact = collision.contacts[0];
        SpawnImpactEffect(contact.point, Quaternion.LookRotation(contact.normal));

        lastCollisionTime = Time.time;
    }

    private void SpawnImpactEffect(Vector3 position, Quaternion rotation)
    {
        if (impactEffects == null || impactEffects.Length == 0) return;

        SCR_ImpactEffect bestEffect = null;
        float shortestTimeRemaining = float.MaxValue;

        // 1. Procura por um efeito desativado
        foreach (var effect in impactEffects)
        {
            if (!effect.IsActive)
            {
                bestEffect = effect;
                break; 
            }

            // 2. Se todos estiverem ativos, rastreia o que está mais perto de acabar
            if (effect.TimeRemaining < shortestTimeRemaining)
            {
                shortestTimeRemaining = effect.TimeRemaining;
                bestEffect = effect;
            }
        }

        // Toca o efeito encontrado (ou o que estava prestes a acabar)
        if (bestEffect != null)
        {
            bestEffect.Play(position, rotation, this.gameObject);
        }
    }
}