using UnityEngine;

public class FogManager : MonoBehaviour
{
    [Header("Target Material")]
    [SerializeField] private Material fogMaterial;

    [Header("Default Profile")]
    [SerializeField] private FogSettingsSO defaultFogSettings;

    // Material Property IDs para melhor performance (evita usar strings a cada frame)
    private static readonly int ColorID = Shader.PropertyToID("_Color");
    private static readonly int MaxDistanceID = Shader.PropertyToID("_MaxDistance");
    private static readonly int StepSizeID = Shader.PropertyToID("_StepSize");
    private static readonly int DensityMultiplierID = Shader.PropertyToID("_DensityMultiplier");
    private static readonly int NoiseOffsetID = Shader.PropertyToID("_NoiseOffset");
    private static readonly int FogNoiseID = Shader.PropertyToID("_FogNoise");
    private static readonly int NoiseTilingID = Shader.PropertyToID("_NoiseTiling");
    private static readonly int DensityThresholdID = Shader.PropertyToID("_DensityThreshold");
    private static readonly int LightContributionID = Shader.PropertyToID("_LightContribution");
    private static readonly int LightScatteringID = Shader.PropertyToID("_LightScattering");

    private void Start()
    {
        if (defaultFogSettings != null)
        {
            Set(defaultFogSettings);
        }
    }

    /// <summary>
    /// Aplica as configurações de um FogSettingsSO no material de neblina.
    /// </summary>
    public void Set(FogSettingsSO settings)
    {
        if (fogMaterial == null || settings == null)
            return;

        // Se estiver desativado, define o multiplicador de densidade para 0 (desativa o efeito visualmente)
        float densityMultiplier = settings.enabled ? settings.densityMultiplier : 0f;

        fogMaterial.SetColor(ColorID, settings.color);
        fogMaterial.SetFloat(MaxDistanceID, settings.maxDistance);
        fogMaterial.SetFloat(StepSizeID, settings.stepSize);
        fogMaterial.SetFloat(DensityMultiplierID, densityMultiplier);
        fogMaterial.SetFloat(NoiseOffsetID, settings.noiseOffset);
        
        fogMaterial.SetTexture(FogNoiseID, settings.fogNoise);
        fogMaterial.SetFloat(NoiseTilingID, settings.noiseTiling);
        fogMaterial.SetFloat(DensityThresholdID, settings.densityThreshold);

        fogMaterial.SetColor(LightContributionID, settings.lightContribution);
        fogMaterial.SetFloat(LightScatteringID, settings.lightScattering);
    }
}