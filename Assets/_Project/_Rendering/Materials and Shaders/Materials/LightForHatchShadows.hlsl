// 1. Pega a luz principal (Sol)
Light mainLight = GetMainLight();
float3 lightAcc = mainLight.color * saturate(dot(NormalWorld, mainLight.direction));

// 2. Soma as luzes adicionais (Postes, lasers, explosões)
uint pixelLightCount = GetAdditionalLightsCount();
for (uint lightIndex = 0u; lightIndex < pixelLightCount; ++lightIndex)
{
    // O argumento implicitamente calcula sombras/atenuação das luzes extras na Unity 6
    Light addLight = GetAdditionalLight(lightIndex, PositionWorld, float4(0,0,0,0));
    float3 lightColor = addLight.color * addLight.distanceAttenuation * addLight.shadowAttenuation;
    lightAcc += lightColor * saturate(dot(NormalWorld, addLight.direction));
}

// Retorna a intensidade média (brilho total recebido)
LightIntensity = saturate((lightAcc.r + lightAcc.g + lightAcc.b) / 3.0);