using UnityEngine;

[CreateAssetMenu(fileName = "NewFogSettings", menuName = "Environment/Fog Settings")]
public class FogSettingsSO : ScriptableObject
{
    [Header("General Settings")]
    public bool enabled = true;
    public Color color = Color.white;
    public float maxDistance = 100f;
    [Range(0.1f, 20f)] public float stepSize = 1f;
    [Range(0f, 10f)] public float densityMultiplier = 1f;
    public float noiseOffset = 0f;

    [Header("Noise Settings")]
    public Texture3D fogNoise;
    public float noiseTiling = 1f;
    [Range(0f, 1f)] public float densityThreshold = 0.1f;

    [Header("Lighting Settings")]
    [ColorUsage(false, true)] public Color lightContribution = Color.white;
    [Range(0f, 1f)] public float lightScattering = 0.2f;
}