using System.Collections.Generic;
using UnityEngine;

public class DamageCar : MonoBehaviour
{
    [Header("Configurações de Dano")]
    [Range(-0.5f, 1f)]
    [SerializeField] private float currentDamage = -0.5f; // -0.5 = Totalmente Limpo, 1 = Destruído
    public float publicCurrentDamage => currentDamage;

    [SerializeField] private List<Renderer> carRenderers;
    [SerializeField] private string shaderDamagePropertyName = "_DamageAmount";

    [Header("Efeitos de Partículas")]
    [SerializeField] private List<ParticleSystem> smokeParticles = new List<ParticleSystem>();
    [SerializeField] private List<ParticleSystem> fireParticles = new List<ParticleSystem>();

    [Header("Configurações de Emissão Máxima")]
    [Tooltip("Multiplicador de emissão base para fumaça quando o dano estiver no máximo.")]
    [SerializeField] private float maxSmokeEmissionRate = 50f;

    [Tooltip("Multiplicador de emissão base para fogo quando o dano estiver no máximo.")]
    [SerializeField] private float maxFireEmissionRate = 30f;

    [Header("Configurações de Instabilidade")]
    [Tooltip("Aumento máximo da instabilidade quando o carro está destruído.")]
    [SerializeField, Range(0f, 1f)] private float maxInstability = 0.30f;

    [Header("Referências Externas (Culling)")]
    [SerializeField] private SCR_RayBasedCarPhysics carPhysics;

    // Cache interno
    private MaterialPropertyBlock _propBlock;
    private List<ParticleSystem> _activeSmokeSystems = new List<ParticleSystem>();
    private List<ParticleSystem> _activeFireSystems = new List<ParticleSystem>();
    private bool _wasVisualsEnabledLastFrame = true;

    void Awake()
    {
        _propBlock = new MaterialPropertyBlock();

        // Filtra e armazena apenas os sistemas válidos
        foreach (var ps in smokeParticles)
        {
            if (ps != null)
                _activeSmokeSystems.Add(ps);
        }

        foreach (var ps in fireParticles)
        {
            if (ps != null)
                _activeFireSystems.Add(ps);
        }

        if (carPhysics == null)
        {
            carPhysics = GetComponent<SCR_RayBasedCarPhysics>();
        }
    }

    void Start()
    {
        UpdateDamageVisuals();
    }

    void Update()
    {
        if (carPhysics != null)
        {
            if (!carPhysics.CanUpdateVisuals)
            {
                if (_wasVisualsEnabledLastFrame)
                {
                    StopAllVisualEffects();
                    _wasVisualsEnabledLastFrame = false;
                }

                return;
            }

            _wasVisualsEnabledLastFrame = true;
        }

        UpdateDamageVisuals();
    }

    private void UpdateDamageVisuals()
    {
        // 1. Atualiza os Shaders via MaterialPropertyBlock
        if (carRenderers != null)
        {
            foreach (Renderer carRenderer in carRenderers)
            {
                if (carRenderer == null)
                    continue;

                carRenderer.GetPropertyBlock(_propBlock);
                _propBlock.SetFloat(shaderDamagePropertyName, currentDamage);
                carRenderer.SetPropertyBlock(_propBlock);
            }
        }

        // 2. Gerenciamento de Fumaça
        float smokeIntensity = Mathf.InverseLerp(0.2f, 1f, currentDamage);
        float currentSmokeRate = smokeIntensity * maxSmokeEmissionRate;

        foreach (var ps in _activeSmokeSystems)
        {
            var emission = ps.emission;
            emission.rateOverTime = currentSmokeRate;
        }

        // 3. Gerenciamento de Fogo
        float fireIntensity = Mathf.InverseLerp(0.65f, 1f, currentDamage);
        float currentFireRate = fireIntensity * maxFireEmissionRate;

        foreach (var ps in _activeFireSystems)
        {
            var emission = ps.emission;
            emission.rateOverTime = currentFireRate;
        }
    }

    private void StopAllVisualEffects()
    {
        foreach (var ps in _activeSmokeSystems)
        {
            var emission = ps.emission;
            emission.rateOverTime = 0f;
        }

        foreach (var ps in _activeFireSystems)
        {
            var emission = ps.emission;
            emission.rateOverTime = 0f;
        }
    }

    #region Sistema de Instabilidade

    /// <summary>
    /// Retorna o dano normalizado de 0 a 1.
    /// 0 = carro intacto
    /// 1 = carro destruído
    /// </summary>
    public float Damage01
    {
        get
        {
            return Mathf.InverseLerp(-0.5f, 1f, currentDamage);
        }
    }

    /// <summary>
    /// Retorna a energia restante do carro.
    /// 1 = energia cheia
    /// 0 = sem energia
    /// </summary>
    public float Health01
    {
        get
        {
            return 1f - Damage01;
        }
    }

    #endregion

    #region Métodos Públicos (Gatilhos)

    public void ApplyDamage(float amount)
    {
        currentDamage = Mathf.Clamp(currentDamage + amount, -0.5f, 1f);
    }

    public void SetDamageLevel(float targetDamage)
    {
        currentDamage = Mathf.Clamp(targetDamage, -0.5f, 1f);
    }

    public void ResetDamage()
    {
        currentDamage = -0.5f;
        UpdateDamageVisuals();
    }

    public float GetCurrentDamage()
    {
        return currentDamage;
    }

    #endregion
}