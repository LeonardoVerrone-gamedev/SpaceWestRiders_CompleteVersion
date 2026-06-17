void ToonShading_float(
    in float3 Normal, 
    in float ToonRampSmoothness, 
    in float4 ClipSpacePos, 
    in float3 WorldPos, 
    in float3 ToonRampTinting, // Mantido para alinhar com o seu Shader Graph
    in float ToonRampOffset, 
    in float ToonRampOffsetPoint, 
    in float Ambient, 
    out float3 ToonRampOutput, 
    out float3 Direction, 
    out float ShadowMask)
{

    // set the shader graph node previews
#ifdef SHADERGRAPH_PREVIEW
    ToonRampOutput = float3(0.5,0.5,0);
    Direction = float3(0.5,0.5,0);
    ShadowMask = 0.0; 
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

    // Guarda a intensidade do Sol (0 na sombra, 1 no sol)
    float lightIntensity = toonRamp;

    // Inicializa o acumulador de cor das luzes adicionais
    float3 extraLights = float3(0,0,0);

    // forward and forward+ lights loop
    uint lightsCount = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(lightsCount)
        
        Light aLight = GetAdditionalLight(lightIndex, WorldPos, half4(1,1,1,1));
        half dExtra = dot(Normal, aLight.direction) * 0.5 + 0.5;
        float shadowAtten = aLight.distanceAttenuation * aLight.shadowAttenuation;
        half toonRampExtra = smoothstep(ToonRampOffsetPoint, ToonRampOffsetPoint + ToonRampSmoothness, dExtra);
        
        float lightContribution = toonRampExtra * shadowAtten;

        // 1. SOMA A COR DA LUZ EXTRA (Multiplicada pela cor real da point light)
        extraLights += (aLight.color * lightContribution);
        
        // 2. ACUMULA NA INTENSIDADE DA MÁSCARA (A point light clareia a região da hachura)
        lightIntensity += lightContribution;
                
    LIGHT_LOOP_END
    
    // Saída de cor total combinada (Sol + Point Lights)
    ToonRampOutput = light.color * (toonRamp + ToonRampTinting) + Ambient + extraLights;
    
    // Direção da luz principal
    Direction = normalize(light.direction);
    
    // Máscara final tratada para os seus nós Step (0 = Sombra total, 1 = Iluminado por algo)
    ShadowMask = saturate(lightIntensity);

#endif
}