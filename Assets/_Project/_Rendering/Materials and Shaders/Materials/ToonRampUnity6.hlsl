void ToonShading_float(in float3 Normal, in float ToonRampSmoothness, in float4 ClipSpacePos, in float3 WorldPos, in float3 ToonRampTinting,
in float ToonRampOffset, in float ToonRampOffsetPoint, in float Ambient, out float3 ToonRampOutput, out float3 Direction, out float ShadowMask)
{

	// set the shader graph node previews
#ifdef SHADERGRAPH_PREVIEW
    ToonRampOutput = float3(0.5,0.5,0);
    Direction = float3(0.5,0.5,0);
    ShadowMask = 0.0; // Preview padrão
#else

    // grab the shadow coordinates
    #if SHADOWS_SCREEN
        half4 shadowCoord = ComputeScreenPos(ClipSpacePos);
    #else
        half4 shadowCoord = TransformWorldToShadowCoord(WorldPos);
    #endif 

    // grab the main light
    #if _MAIN_LIGHT_SHADOWS_CASCADE || _MAIN_LIGHT_SHADOWS
        Light light = GetMainLight(shadowCoord);
    #else
        Light light = GetMainLight();
    #endif

    // dot product for toonramp
    half d = dot(Normal, light.direction) * 0.5 + 0.5;
    
    // toonramp in a smoothstep
    half toonRamp = smoothstep(ToonRampOffset, ToonRampOffset + ToonRampSmoothness, d);
    
    // multiply with main light shadows;
    toonRamp *= light.shadowAttenuation;

    // --- VARIÁVEL NOVA PARA A MÁSCARA ---
    // Começa guardando o fator de iluminação do Sol (0 no escuro/sombra, 1 no sol pleno)
    float lightIntensity = toonRamp;

    float3 extraLights = float3(0,0,0);

    // create inputdata struct to use in LIGHT_LOOP
    InputData inputData = (InputData)0;
    inputData.positionWS = WorldPos;
    inputData.normalWS = Normal;
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(WorldPos);
    float4 screenPos = float4(ClipSpacePos.x, (_ScaledScreenParams.y - ClipSpacePos.y), 0, 0);
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(screenPos);

    // forward and forward+ lights loop
    uint lightsCount = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(lightsCount)
        
        Light aLight = GetAdditionalLight(lightIndex, WorldPos, half4(1,1,1,1));
        half dExtra = dot(Normal, aLight.direction) * 0.5 + 0.5;
        float shadowAtten = aLight.distanceAttenuation * aLight.shadowAttenuation;
        half toonRampExtra = smoothstep(ToonRampOffsetPoint, ToonRampOffsetPoint + ToonRampSmoothness, dExtra);
        
        // Soma a cor das luzes extras
        extraLights += (toonRampExtra * aLight.color * shadowAtten);
        
        // --- ADICIONA AS LUZES EXTRAS NA INTENSIDADE ---
        // Se uma point light bater aqui, essa região ganha luz (sobe o valor)
        lightIntensity += (toonRampExtra * shadowAtten);
                
    LIGHT_LOOP_END
    
    // Saídas padrão de cor que você já usava
    ToonRampOutput = light.color * (toonRamp + ToonRampTinting) + Ambient + extraLights;
    Direction = normalize(light.direction);
    
    // --- O PULO DO GATO: A SAÍDA DA MÁSCARA DE SOMBRA ---
    // Trava o valor entre 0 e 1. 
    // 0 = Escuro total (Sombra absoluta de todas as luzes)
    // 1 = Iluminado (Seja pelo sol ou por um poste)
    ShadowMask = saturate(lightIntensity);

#endif
}