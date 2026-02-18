using UnityEngine;
using Unity.Cinemachine;
using System.Collections;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using System.Collections.Generic;

public class CameraController : MonoBehaviour
{
    [SerializeField] bool useCinemachine = true;
    [SerializeField] CinemachineCamera vCam;
    [SerializeField] CinemachineCamera turbo_VCam;

    [SerializeField] Camera cam;

    private Transform player;
    private Rigidbody playerRB;

    private SCR_CarInput carInput;

    public Transform cameraTarget => player;
    
    //  SISTEMA RADICAL: Estado único e transições imediatas com blending nativo
    public enum CameraType { Default, Turbo }
    [SerializeField] private CameraType currentCameraType = CameraType.Default;
    
    [Header("Configurações de Blending - Sistema Simplificado")]
    [Tooltip("Usar blending nativo do Cinemachine em vez de coroutines")]
    public bool useNativeBlending = true;
    
    [Tooltip("Tempo de blend para transições normais")]
    public float normalBlendTime = 0.3f;
    
    [Tooltip("Tempo de blend para transições de turbo (mais rápido)")]
    public float turboBlendTime = 0.15f;
    
    [Tooltip("Tempo de blend para transições de freio (mais lento)")]
    public float brakeBlendTime = 0.4f;

    //  CONTROLE DE BLOQUEIO para evitar flickering
    private bool transitionLocked = false;
    private float transitionLockTime = 0f;
    private const float MIN_TRANSITION_INTERVAL = 0.1f;

    [SerializeField] Volume[] volumes;
    private Volume defaultVolume;
    private Volume turboVolume;

    //  CONFIGURAÇÕES DE VELOCIDADE E EFEITOS
    [Header("Configurações de Velocidade - Câmera Default")]
    [Tooltip("Velocidade mínima para iniciar efeitos (km/h)")]
    public float minSpeedForEffects = 100f;
    
    [Tooltip("Velocidade máxima para efeitos completos (km/h)")]
    public float maxSpeedForEffects = 220f;
    
    [Header("Configurações da Câmera Default")]
    [Tooltip("FOV mínimo (velocidade baixa)")]
    public float minFOV = 50f;
    
    [Tooltip("FOV máximo (alta velocidade)")]
    public float maxFOV = 75f;
    
    [Tooltip("Distância da câmera mínima (alta velocidade)")]
    public float minCameraDistance = 9.5f;
    
    [Tooltip("Distância da câmera máxima (velocidade baixa)")]
    public float maxCameraDistance = 12f;

    [Header("Configurações de Vignette - Efeito de Velocidade")]
    [Tooltip("Intensidade mínima do vignette")]
    public float minVignetteIntensity = 0.2f;
    
    [Tooltip("Intensidade máxima do vignette")]
    public float maxVignetteIntensity = 0.5f;
    
    [Tooltip("Suavidade mínima do vignette")]
    public float minVignetteSmoothness = 0.6f;
    
    [Tooltip("Suavidade máxima do vignette")]
    public float maxVignetteSmoothness = 1f;

    [Header("Configurações de Chromatic Aberration - Efeito de Velocidade")]
    [Tooltip("Intensidade mínima do chromatic aberration")]
    public float minChromaticIntensity = 0.3f;
    
    [Tooltip("Intensidade máxima do chromatic aberration")]
    public float maxChromaticIntensity = 0.6f;

    [Header("Configurações de Color Adjustments - Efeito de Velocidade")]
    [Tooltip("Exposição mínima")]
    public float minPostExposure = 2.5f;
    
    [Tooltip("Exposição máxima")]
    public float maxPostExposure = 3f;
    
    [Tooltip("Saturação mínima")]
    public float minSaturation = 20f;
    
    [Tooltip("Saturação máxima")]
    public float maxSaturation = 75f;

    [Header("Configurações de Motion Blur - Efeito de Velocidade")]
    [Tooltip("Intensidade mínima do motion blur")]
    public float minMotionBlurIntensity = 0.5f;
    
    [Tooltip("Intensidade máxima do motion blur")]
    public float maxMotionBlurIntensity = 0.65f;

    [Header("Configurações de Lens Distortion")]
    [Tooltip("Intensidade mínima do Lens Distortion")]
    public float minLensDistortionntensity = 0f;
    
    [Tooltip("Intensidade máxima do motion blur")]
    public float maxLensDistortionntensity = 0.5f;

    //  SISTEMA DE SUAVIZAÇÃO
    [Header("Configurações de Suavização - Efeitos de Velocidade")]
    [Tooltip("Tempo para suavizar mudanças de FOV")]
    public float fovSmoothTime = 0.5f;
    
    [Tooltip("Tempo para suavizar mudanças de distância da câmera")]
    public float distanceSmoothTime = 0.5f;
    
    [Tooltip("Tempo para suavizar efeitos de volume")]
    public float volumeSmoothTime = 0.3f;

    [Header("Configurações de Aceleração/Desaceleração")]
    [Tooltip("Fator de aceleração dos efeitos (mais rápido para aumentar)")]
    public float accelerationResponse = 2f;
    
    [Tooltip("Fator de desaceleração dos efeitos (mais lento para diminuir)")]
    public float decelerationResponse = 1f;

    //  REFERÊNCIAS E VARIÁVEIS DE CONTROLE
    private float currentFOV;
    private CinemachinePositionComposer thirdPersonFollow;
    private float currentSpeedKmh = 0f;
    private float rawSpeedFactor = 0f;
    private float smoothedSpeedFactor = 0f;

    //  VARIÁVEIS PARA SUAVIZAÇÃO
    private float currentFOVVelocity = 0f;
    private float currentDistanceVelocity = 0f;
    
    // Para suavizar os efeitos de volume
    private float currentVignetteIntensityVelocity = 0f;
    private float currentVignetteSmoothnessVelocity = 0f;
    private float currentChromaticIntensityVelocity = 0f;
    private float currentPostExposureVelocity = 0f;
    private float currentSaturationVelocity = 0f;
    private float currentMotionBlurIntensityVelocity = 0f;
    private float currentLensDistortionIntensityVelocity = 0f;

    //  CACHE DOS VALORES ATUAIS PARA SUAVIZAÇÃO
    private float currentVignetteIntensity = 0f;
    private float currentVignetteSmoothness = 0f;
    private float currentChromaticIntensity = 0f;
    private float currentBloomThreshold = 0f;
    private float currentBloomIntensity = 0f;
    private float currentPostExposure = 0f;
    private float currentSaturation = 0f;
    private float currentMotionBlurIntensity = 0f;
    private float currentLensDistortionIntensity = 0f;

    [Header("Física Cache")]
    private SCR_RayBasedCarPhysics carPhysics; // Cache da referência de física

    [Header("Sistema de Tremor de Câmera")]
    [SerializeField] private CinemachineImpulseSource impulseSource;
    [SerializeField] private bool enableImpulseShake = true;

    [Header("Shake por Situação")]
    [Tooltip("Shake ao acelerar")]
    public ShakeProfile accelerationShake;
    [Tooltip("Shake ao frear")]
    public ShakeProfile brakingShake;
    [Tooltip("Shake em curvas")]
    public ShakeProfile turningShake;
    [Tooltip("Shake no turbo")]
    public ShakeProfile turboShake;
    [Tooltip("Shake em saltos")]
    public ShakeProfile jumpShake;
    [Tooltip("Shake em colisões")]
    public ShakeProfile collisionShake;

    [System.Serializable]
    public class ShakeProfile
    {
        public float amplitude = 0.5f;
        public float frequency = 1f;
        public float duration = 0.3f;
        public Vector3 direction = Vector3.one;
    }

    private ShakePriority currentShakePriority = ShakePriority.Low;
    private float shakeLockUntil = 0f;

    private float lastShakeTime = 0f;
    private bool wasGrounded = true;
    private float airTime = 0f;
    private Vector3 lastVelocity;
    private float velocityChange;
    private CarType currentCarType;

    [Header("Sistema de Noise Contínuo")]
    [SerializeField] private CinemachineBasicMultiChannelPerlin defaultNoise;
    [SerializeField] private CinemachineBasicMultiChannelPerlin turboNoise;
    [SerializeField] CinemachineRotateWithFollowTarget defaultRotate;
    [SerializeField] CinemachineRotateWithFollowTarget turboRotate;
    
    [Header("Configurações de Noise por Tipo de Carro")]
    [Tooltip("Amplitude máxima do noise para carro clássico")]
    public float classicMaxAmplitude = 0.3f;
    
    [Tooltip("Frequência máxima do noise para carro clássico")]
    public float classicMaxFrequency = 1.2f;
    
    [Tooltip("Amplitude máxima do noise para hover")]
    public float hoverMaxAmplitude = 0.15f;
    
    [Tooltip("Frequência máxima do noise para hover")]
    public float hoverMaxFrequency = 0.8f;
    
    [Tooltip("Velocidade mínima para iniciar noise (km/h)")]
    public float minSpeedForNoise = 30f;
    
    [Tooltip("Velocidade para noise máximo (km/h)")]
    public float maxSpeedForNoise = 200f;
    
    [Tooltip("Curva de intensidade do noise baseado na velocidade")]
    public AnimationCurve speedNoiseCurve;
    
    [Tooltip("Curva de intensidade do noise baseado na aceleração")]
    public AnimationCurve accelerationNoiseCurve;
    
    [Tooltip("Multiplicador de noise durante turbo")]
    public float turboNoiseMultiplier = 0.5f; // Menos noise durante turbo (mais estável)
    
    [Tooltip("Multiplicador de noise durante drift")]
    public float driftNoiseMultiplier = 1.5f; // Mais noise durante drift
    
    [Tooltip("Tempo de suavização do noise")]
    public float noiseSmoothTime = 0.3f;
    
    // Variáveis para controle do noise
    private float currentNoiseAmplitude = 0f;
    private float currentNoiseFrequency = 0f;
    private float noiseAmplitudeVelocity = 0f;
    private float noiseFrequencyVelocity = 0f;
    private float lastSpeed = 0f;
    private float currentAcceleration = 0f;
    private float accelerationNoiseIntensity = 0f;

    private float impactImpulseCoolDown = 1.5f;
    private float lastImpactPulseTime;

    [SerializeField] List<CinemachineRecomposer> cinemachineRecomposer;
    [SerializeField] float dutchMultiplier = 10f;


    void Start()
    {
        //  CONFIGURAÇÃO DOS VOLUMES
        if (volumes != null && volumes.Length >= 2)
        {
            defaultVolume = volumes[0];
            turboVolume = volumes[1];
            
            // Configuração inicial dos volumes
            UpdateVolumes(currentCameraType);
            InitializeVolumeValues();
        }

        //  OBTER REFERÊNCIAS DOS COMPONENTES DA CÂMERA
        if (vCam != null)
        {
            currentFOV = vCam.Lens.FieldOfView;
            thirdPersonFollow = vCam.GetComponent<CinemachinePositionComposer>();
        }

        //cinemachineRecomposer.Add(vCam.GetComponent<CinemachineRecomposer>());//index 1
        //if(turbo_VCam != null) cinemachineRecomposer.Add(turbo_VCam.GetComponent<CinemachineRecomposer>()); //index 2

        if (impulseSource == null)
        {
            impulseSource = GetComponent<CinemachineImpulseSource>();
            if (impulseSource == null)
            {
                impulseSource = gameObject.AddComponent<CinemachineImpulseSource>();
            }
        }
    
       if (impulseSource != null)
        {
            impulseSource.ImpulseDefinition.ImpulseShape =
            CinemachineImpulseDefinition.ImpulseShapes.Bump;

            impulseSource.ImpulseDefinition.DissipationDistance = 100f;

            // Envelope
            impulseSource.ImpulseDefinition.TimeEnvelope.DecayTime = 0.3f;
            impulseSource.ImpulseDefinition.TimeEnvelope.SustainTime = 0.1f;
        }

        lastVelocity = Vector3.zero;
    }

    void Update()
    {
        HandlePhysicsBasedEffects();
        UpdateContinuousNoise();
        HandleRotateDamping();
    }

    void HandleRotateDamping()
    {
        float rotateDamping = carPhysics.IsDrifting() ? 3f : 1f;
        defaultRotate.Damping = rotateDamping; turboRotate.Damping = rotateDamping;
    }

    private void HandlePhysicsBasedEffects()
    {
        if (player == null || carPhysics == null) return;

        // 2. Atualiza FOV e Efeitos de Volume baseados na velocidade km/h da física
        float speedKmh = playerRB != null ? playerRB.linearVelocity.magnitude * 3.6f : 0f;
        UpdateCameraBasedOnSpeed(speedKmh);

        // 3. Gerenciamento automático de Estados de Câmera
        ManageCameraStates();
    }

    private void ManageCameraStates()
    {
        // Prioridade 1: Turbo
        if (carPhysics.IsTurboActive())
        {
            if (currentCameraType != CameraType.Turbo)
            {
                SetCam(CameraType.Turbo);
            }
        }
        else
        {
            if (currentCameraType != CameraType.Default)
            {
                SetCam(CameraType.Default);
            }
        }
    }

    public void SetTarget(Transform newTarget)
    {
        player = newTarget;
        playerRB = player?.GetComponent<Rigidbody>();

        // ATUALIZAÇÃO DO CACHE DE FÍSICA SEMPRE QUE O PLAYER MUDA
        if (player != null)
        {
            carPhysics = player.GetComponent<SCR_RayBasedCarPhysics>();
            // Se o script de física estiver no pai ou filho, ajuste:
            if (carPhysics == null) carPhysics = player.GetComponentInParent<SCR_RayBasedCarPhysics>();

            carInput = carPhysics.gameObject.GetComponent<SCR_CarInput>();
        }
        else
        {
            carPhysics = null;
        }

        SetupCarEventListeners();

        if (useCinemachine && vCam != null)
        {
            vCam.Follow = player.transform.Find("CameraTarget");
            if (turbo_VCam != null) turbo_VCam.Follow = player.transform.Find("CameraTarget");
            
            ResetAllCameras();
            vCam.Priority = 10;
            currentCameraType = CameraType.Default;
            UpdateVolumes(currentCameraType);
        }
    }

    public void SetChannel(int playerIndex)
    {
        int impulseChannel = playerIndex+1;
        int impulseMask = playerIndex+1;
        // Converte o index (0, 1) para uma Layer Mask de canais (1, 2, 4...)
        // Canal 0 = 1, Canal 1 = 2, Canal 2 = 4
        OutputChannels channelMask = (OutputChannels)(1 << playerIndex);

        // 1. Configura o Brain para ouvir apenas esse canal
        var brain = GetComponentInChildren<CinemachineBrain>();
        if (brain != null)
        {
            brain.ChannelMask = channelMask;
            impulseSource.ImpulseDefinition.ImpulseChannel = impulseChannel;

            if (vCam.TryGetComponent<CinemachineImpulseListener>(out var listener))
            listener.ChannelMask = impulseChannel;

            if (turbo_VCam != null)
            {
                if (turbo_VCam.TryGetComponent<CinemachineImpulseListener>(out var tListener))
                    tListener.ChannelMask = impulseChannel;
            }
        }

        // 2. Configura todas as Virtual Cameras do Prefab para esse canal
        vCam.OutputChannel = channelMask;
        if (turbo_VCam != null) turbo_VCam.OutputChannel = channelMask;

        int layerP1 = LayerMask.NameToLayer("VolumeP1");
        int layerP2 = LayerMask.NameToLayer("VolumeP2");
        int targetLayer = (playerIndex == 0) ? layerP1 : layerP2;

        // 2. Aplica a layer nos objetos de Volume
        if (defaultVolume != null) defaultVolume.gameObject.layer = targetLayer;
        if (turboVolume != null) turboVolume.gameObject.layer = targetLayer;

        foreach(Volume volume in volumes)
        {
            volume.gameObject.layer = targetLayer;
        }

        // 3. Configura a Volume Mask da Camera física via URP Data
        var additionalCamData = cam.GetUniversalAdditionalCameraData();
        if (additionalCamData != null)
        {
            // A câmera só vai processar volumes que estejam na targetLayer
            additionalCamData.volumeLayerMask = (1 << targetLayer);
        }
    }

    public void EnableSplitScreen(bool enable, bool up)
    {
        Camera cam = GetComponentInChildren<Camera>(); // Pega a Unity Camera do prefab
        if (cam == null) return;

        if (!enable)
        {
            // Full Screen
            cam.rect = new Rect(0, 0, 1, 1);
        }
        else
        {
            if (up)
            {
                // Metade Superior (P1)
                cam.rect = new Rect(0, 0.5f, 1, 0.5f);
            }
            else
            {
                // Metade Inferior (P2)
                cam.rect = new Rect(0, 0, 1, 0.5f);
            }
        }
    }

    //  INICIALIZAR VALORES DOS VOLUMES
    private void InitializeVolumeValues()
    {
        if (defaultVolume != null && defaultVolume.profile != null)
        {
            if (defaultVolume.profile.TryGet<Vignette>(out var vignette))
            {
                currentVignetteIntensity = vignette.intensity.value;
                currentVignetteSmoothness = vignette.smoothness.value;
            }

            if (defaultVolume.profile.TryGet<ChromaticAberration>(out var chromaticAberration))
            {
                currentChromaticIntensity = chromaticAberration.intensity.value;
            }

            if (defaultVolume.profile.TryGet<Bloom>(out var bloom))
            {
                currentBloomThreshold = bloom.threshold.value;
                currentBloomIntensity = bloom.intensity.value;
            }

            if (defaultVolume.profile.TryGet<ColorAdjustments>(out var colorAdjustments))
            {
                currentPostExposure = colorAdjustments.postExposure.value;
                currentSaturation = colorAdjustments.saturation.value;
            }

            if (defaultVolume.profile.TryGet<MotionBlur>(out var motionBlur))
            {
                currentMotionBlurIntensity = motionBlur.intensity.value;
            }
            if (defaultVolume.profile.TryGet<LensDistortion>(out var lensDistortion))
            {
                currentLensDistortionIntensity = lensDistortion.intensity.value;
            }
        }
    }

    //  ATUALIZAR CÂMERA BASEADO NA VELOCIDADE COM SUAVIZAÇÃO
    public void UpdateCameraBasedOnSpeed(float speedKmh)
    {
        if (!useCinemachine || currentCameraType != CameraType.Default || vCam == null) return;
        
        currentSpeedKmh = speedKmh;
        
        // Calcular fator de velocidade bruto
        rawSpeedFactor = 0f;
        if (currentSpeedKmh >= minSpeedForEffects)
        {
            rawSpeedFactor = Mathf.Clamp01((currentSpeedKmh - minSpeedForEffects) / (maxSpeedForEffects - minSpeedForEffects));
        }

        //  SUAVIZAÇÃO INTELIGENTE: Resposta diferente para aceleração vs desaceleração
        float responseTime = rawSpeedFactor > smoothedSpeedFactor ? accelerationResponse : decelerationResponse;
        smoothedSpeedFactor = Mathf.Lerp(smoothedSpeedFactor, rawSpeedFactor, responseTime * Time.deltaTime);

        ApplyCameraInterpolation();
        ApplyVolumeEffects();
        UpdateDutch();
    }

    private float currentDutchVelocity = 0f;
    private float currentDutch = 0f;

    private void UpdateDutch()
    {
        float dot = Vector3.Dot(player.right, Vector3.up);
        float targetDutch = dutchMultiplier * dot;
        
        // Suavizar o Dutch para evitar trancos
        currentDutch = Mathf.SmoothDamp(currentDutch, targetDutch, ref currentDutchVelocity, 0.2f);
        
        foreach(CinemachineRecomposer recom in cinemachineRecomposer) 
            recom.Dutch = currentDutch;
    }

    //  APLICAR INTERPOLAÇÃO SUAVE DA CÂMERA
    private void ApplyCameraInterpolation()
    {
        if (vCam == null) return;

        //  SUAVIZAÇÃO DE FOV com Mathf.SmoothDamp
        float targetFOV = Mathf.Lerp(minFOV, maxFOV, smoothedSpeedFactor);
        var lens = vCam.Lens;
        lens.FieldOfView = Mathf.SmoothDamp(lens.FieldOfView, targetFOV, ref currentFOVVelocity, fovSmoothTime);
        vCam.Lens = lens;

        //  SUAVIZAÇÃO DE DISTÂNCIA com Mathf.SmoothDamp
        if (thirdPersonFollow != null)
        {
            float targetDistance = Mathf.Lerp(maxCameraDistance, minCameraDistance, smoothedSpeedFactor);
            float currentDistance = thirdPersonFollow.CameraDistance;
            
            // 1. LIMITA A MUDANÇA MÁXIMA POR FRAME
            float maxChangePerFrame = 0.1f; // Máx 10cm por frame
            float rawNewDistance = Mathf.SmoothDamp(currentDistance, targetDistance, 
                                                ref currentDistanceVelocity, distanceSmoothTime);
            
            // 2. CLAMPA PARA EVITAR PULOS
            float newDistance = Mathf.Clamp(rawNewDistance, 
                                        currentDistance - maxChangePerFrame, 
                                        currentDistance + maxChangePerFrame);
            
            // 3. GARANTE QUE FICA DENTRO DOS LIMITES
            newDistance = Mathf.Clamp(newDistance, minCameraDistance, maxCameraDistance);
            
            thirdPersonFollow.CameraDistance = newDistance;
        }
    }

    //  APLICAR EFEITOS DE VOLUME COM SUAVIZAÇÃO
    private void ApplyVolumeEffects()
    {
        if (defaultVolume == null || !defaultVolume.gameObject.activeInHierarchy) return;

        //  CALCULAR VALORES ALVO
        float targetVignetteIntensity = Mathf.Lerp(minVignetteIntensity, maxVignetteIntensity, smoothedSpeedFactor);
        float targetVignetteSmoothness = Mathf.Lerp(minVignetteSmoothness, maxVignetteSmoothness, smoothedSpeedFactor);
        float targetChromaticIntensity = Mathf.Lerp(minChromaticIntensity, maxChromaticIntensity, smoothedSpeedFactor);
        float targetPostExposure = Mathf.Lerp(minPostExposure, maxPostExposure, smoothedSpeedFactor);
        float targetSaturation = Mathf.Lerp(minSaturation, maxSaturation, smoothedSpeedFactor);
        float targetMotionBlurIntensity = Mathf.Lerp(minMotionBlurIntensity, maxMotionBlurIntensity, smoothedSpeedFactor);
        float targetLensDistortionIntensity = Mathf.Lerp(minLensDistortionntensity, maxLensDistortionntensity, smoothedSpeedFactor);

        //  APLICAR SUAVIZAÇÃO A CADA EFEITO
        if (defaultVolume.profile.TryGet<Vignette>(out var vignette))
        {
            currentVignetteIntensity = Mathf.SmoothDamp(currentVignetteIntensity, targetVignetteIntensity, ref currentVignetteIntensityVelocity, volumeSmoothTime);
            currentVignetteSmoothness = Mathf.SmoothDamp(currentVignetteSmoothness, targetVignetteSmoothness, ref currentVignetteSmoothnessVelocity, volumeSmoothTime);
            
            vignette.intensity.value = currentVignetteIntensity;
            vignette.smoothness.value = currentVignetteSmoothness;
        }

        if (defaultVolume.profile.TryGet<ChromaticAberration>(out var chromaticAberration))
        {
            currentChromaticIntensity = Mathf.SmoothDamp(currentChromaticIntensity, targetChromaticIntensity, ref currentChromaticIntensityVelocity, volumeSmoothTime);
            chromaticAberration.intensity.value = currentChromaticIntensity;
        }

        if (defaultVolume.profile.TryGet<ColorAdjustments>(out var colorAdjustments))
        {
            currentPostExposure = Mathf.SmoothDamp(currentPostExposure, targetPostExposure, ref currentPostExposureVelocity, volumeSmoothTime);
            currentSaturation = Mathf.SmoothDamp(currentSaturation, targetSaturation, ref currentSaturationVelocity, volumeSmoothTime);
            
            colorAdjustments.postExposure.value = currentPostExposure;
            colorAdjustments.saturation.value = currentSaturation;
        }

        if (defaultVolume.profile.TryGet<MotionBlur>(out var motionBlur))
        {
            currentMotionBlurIntensity = Mathf.SmoothDamp(currentMotionBlurIntensity, targetMotionBlurIntensity, ref currentMotionBlurIntensityVelocity, volumeSmoothTime);
            motionBlur.intensity.value = currentMotionBlurIntensity;
        }

         if (defaultVolume.profile.TryGet<LensDistortion>(out var lensDistortion))
        {
            currentLensDistortionIntensity = Mathf.SmoothDamp(currentLensDistortionIntensity, targetLensDistortionIntensity, ref currentLensDistortionIntensityVelocity, volumeSmoothTime);
            lensDistortion.intensity.value = currentLensDistortionIntensity;
        }
    }

    //  MÉTODO PRINCIPAL PARA TROCAR CÂMERAS
    public void SetCam(CameraType type)
    {
        if (!useCinemachine || currentCameraType == type) return;
        
        //  BLOQUEIO ANTI-FLICKER
        if (transitionLocked && Time.time < transitionLockTime + MIN_TRANSITION_INTERVAL)
            return;

        //  DETERMINA prioridades com base no tipo
        switch (type)
        {
            case CameraType.Turbo:
                SwitchToCamera(turbo_VCam, vCam, turboBlendTime);
                break;
                
            case CameraType.Default:
                SwitchToCamera(vCam, turbo_VCam, normalBlendTime);
                break;
        }
        
        currentCameraType = type;
        UpdateVolumes(currentCameraType);
        transitionLocked = true;
        transitionLockTime = Time.time;
        
        //  DESBLOQUEIA após um frame
        StartCoroutine(UnlockAfterFrame());
    }

    //  MÉTODO SIMPLIFICADO: Ativa uma câmera e desativa as outras
    private void SwitchToCamera(CinemachineCamera activeCam, CinemachineCamera deactiveCam1, float blendTime)
    {
        if (activeCam == null) return;

        //  CONFIGURA blending time se suportado
        if (useNativeBlending)
        {
            // Para transições suaves, configure o blending no CinemachineBrain
            // Isso é feito no componente CinemachineBrain da câmera principal
        }

        //  ATIVA a câmera desejada
        activeCam.gameObject.SetActive(true);
        activeCam.Priority = 10;

        //  DESATIVA outras câmeras
        if (deactiveCam1 != null)
        {
            deactiveCam1.Priority = 0;
        }
    }

    //  ATUALIZA OS VOLUMES DE ACORDO COM O TIPO DE CÂMERA
    private void UpdateVolumes(CameraType cameraType)
    {
        if (defaultVolume == null || turboVolume == null) return;

        switch (cameraType)
        {
            case CameraType.Turbo:
                //  TURBO: Ativa apenas o volume turbo
                defaultVolume.gameObject.SetActive(false);
                turboVolume.gameObject.SetActive(true);
                //  RESET SUAVE quando voltar para default
                if (smoothedSpeedFactor > 0.1f)
                {
                    SmoothResetEffects();
                }
                break;
                
            case CameraType.Default:
                //  DEFAULT e BRAKE: Ativa apenas o volume default
                defaultVolume.gameObject.SetActive(true);
                turboVolume.gameObject.SetActive(false);
                break;
        }
    }

    //  MÉTODO PARA RESET SUAVE DOS EFEITOS
    public void SmoothResetEffects()
    {
        smoothedSpeedFactor = 0f;
        rawSpeedFactor = 0f;
    }

    //  COROUTINE para desbloquear após um frame
    private IEnumerator UnlockAfterFrame()
    {
        yield return new WaitForEndOfFrame();
        transitionLocked = false;
    }

    //  MÉTODO PARA CASOS CRÍTICOS (turbo)
    public void SetTurboCamera(bool turboActive)
    {
        if (!useCinemachine) return;

        var targetType = turboActive ? CameraType.Turbo : CameraType.Default;
        
        //  TURBO tem prioridade absoluta e transição imediata
        if (targetType == CameraType.Turbo)
        {
            ForceSwitchToCamera(turbo_VCam, vCam);
            currentCameraType = CameraType.Turbo;
        }
        else if (currentCameraType == CameraType.Turbo) // Só volta se estava no turbo
        {
            ForceSwitchToCamera(vCam, turbo_VCam);
            currentCameraType = CameraType.Default;
        }
        
        UpdateVolumes(currentCameraType);
    }

    //  MÉTODO FORÇADO para turbo (sem bloqueios)
    private void ForceSwitchToCamera(CinemachineCamera activeCam, CinemachineCamera deactiveCam1)
    {
        if (activeCam == null) return;

        //  ATIVAÇÃO IMEDIATA
        activeCam.gameObject.SetActive(true);
        activeCam.Priority = 10;

        //  DESATIVAÇÃO IMEDIATA
        if (deactiveCam1 != null)
        {
            deactiveCam1.Priority = 0;
        }
    }

    private void ResetAllCameras()
    {
        if (vCam != null)
        {
            vCam.Priority = 10;
        }
        if (turbo_VCam != null)
        {
            turbo_VCam.Priority = 0;
        }
        
        UpdateVolumes(currentCameraType);
    }

    private CinemachineCamera GetCameraByType(CameraType type)
    {
        switch (type)
        {
            case CameraType.Default: return vCam;
            case CameraType.Turbo: return turbo_VCam;
            default: return vCam;
        }
    }

    #region shake

    // MÉTODO PRINCIPAL: Gerar shake com prioridade
    public void TryGenerateShake(float amplitude, float duration, float frequency, Vector3 direction, ShakePriority priority)
    {
        if (!CanPlayShake(priority, duration)) return;

        GenerateShake(amplitude, duration, frequency, direction);
    }

    // Método base para gerar shake
    public void GenerateShake(float amplitude, float duration, float frequency, Vector3 direction)
    {
        if (!enableImpulseShake || impulseSource == null) return;

        var def = impulseSource.ImpulseDefinition;

        def.AmplitudeGain = amplitude;
        def.FrequencyGain = frequency;
        
        // Ajuste dinâmico do envelope baseado no perfil
        // Para ser sutil, o Sustain (pico) deve ser curto
        def.TimeEnvelope.SustainTime = duration * 0.1f;
        def.TimeEnvelope.DecayTime = duration * 0.9f;

        // Isso faz com que a direção configurada no perfil (Ex: 0,1,0) 
        // seja respeitada fielmente
        impulseSource.GenerateImpulse(direction);
    }

    // Método para colisões
    public void GenerateCollisionShake(float impactForce, Vector3 direction)
    {
        if (!enableImpulseShake || !CanGenerateImpactShake()) return;

        float intensity = Mathf.Clamp(impactForce / 30f, 0.05f, 0.8f);

        TryGenerateShake(
            collisionShake.amplitude * intensity,
            collisionShake.duration * Mathf.Min(intensity, 2f),
            collisionShake.frequency,
            direction.normalized,
            ShakePriority.Critical
        );

        lastImpactPulseTime = Time.time;
    }

    private bool CanGenerateImpactShake()
    {
        // Verifica se passou tempo suficiente desde o último impacto
        return Time.time >= lastImpactPulseTime + impactImpulseCoolDown;
    }

    // Prioridade centralizada de shake
    private bool CanPlayShake(ShakePriority priority, float duration)
    {
        // Bloqueia se shake atual é maior ou igual
        if (Time.time < shakeLockUntil && priority <= currentShakePriority)
            return false;

        currentShakePriority = priority;
        shakeLockUntil = Time.time + duration * 0.8f;

        return true;
    }

    #endregion


    public CameraType GetCurrentCameraType() => currentCameraType;
    public bool IsTransitioning() => transitionLocked;

    #region shake events

    private void SetupCarEventListeners()
    {
        if (carPhysics == null) return;

        carPhysics.OnTurboStart += HandleTurboStart;
        carPhysics.OnTurboEnd   += HandleTurboEnd;

        carPhysics.OnCollision  += HandleCollision;
        carPhysics.OnLand       += HandleLanding;
    }

    private void HandleTurboStart()
    {
        GenerateShake(
            turboShake.amplitude * 2f,
            turboShake.duration,
            turboShake.frequency,
            Vector3.forward
        );
    }

    private void HandleTurboEnd()
    {
        // opcional: shake de “queda de potência”
    }

    private void HandleCollision(float force, Vector3 direction)
    {
        GenerateCollisionShake(force, direction);
    }

    private void HandleLanding(float airtime)
    {
        if (carPhysics.carType == CarType.hover)
        {
            return; // Nenhum shake para hover
        }

        GenerateShake(
            jumpShake.amplitude * Mathf.Clamp(airtime, 0.5f, 2f),
            jumpShake.duration,
            jumpShake.frequency,
            Vector3.up
        );
    }

    #endregion

    private void OnDisable()
    {
        if (carPhysics == null) return;

        carPhysics.OnTurboStart -= HandleTurboStart;
        carPhysics.OnTurboEnd   -= HandleTurboEnd;

        carPhysics.OnCollision  -= HandleCollision;
        carPhysics.OnLand       -= HandleLanding;
    }

    private void UpdateContinuousNoise()
    {
        if (playerRB == null || carPhysics == null) return;
        
        // Calcular velocidade atual
        Vector3 localVelocity = playerRB.transform.InverseTransformDirection(playerRB.linearVelocity);
        float currentSpeed = Mathf.Abs(localVelocity.z * 3.6f);
        
        // Calcular aceleração (m/s²)
        currentAcceleration = (currentSpeed - lastSpeed) / Time.deltaTime;
        lastSpeed = currentSpeed;
        
        // Determinar qual noise está ativo
        CinemachineBasicMultiChannelPerlin activeNoise = 
            (currentCameraType == CameraType.Turbo && turboNoise != null) ? 
            turboNoise : defaultNoise;
            
        if (activeNoise == null) return;
        
        // Calcular intensidade baseada na velocidade
        float speedNoiseIntensity = CalculateSpeedNoiseIntensity(currentSpeed);
        
        // Calcular intensidade baseada na aceleração
        accelerationNoiseIntensity = CalculateAccelerationNoiseIntensity(currentAcceleration);
        
        // Combinar intensidades (velocidade + aceleração)
        float totalNoiseIntensity = Mathf.Max(speedNoiseIntensity, accelerationNoiseIntensity);
        
        // Aplicar modificadores baseados no estado do carro
        float finalIntensity = ApplyStateModifiers(totalNoiseIntensity);
        
        // Calcular valores alvo de amplitude e frequência
        float targetAmplitude, targetFrequency;
        CalculateTargetNoiseValues(currentCarType, finalIntensity, out targetAmplitude, out targetFrequency);
        
        // Suavizar transições
        currentNoiseAmplitude = Mathf.SmoothDamp(
            currentNoiseAmplitude, 
            targetAmplitude, 
            ref noiseAmplitudeVelocity, 
            noiseSmoothTime
        );
        
        currentNoiseFrequency = Mathf.SmoothDamp(
            currentNoiseFrequency, 
            targetFrequency, 
            ref noiseFrequencyVelocity, 
            noiseSmoothTime
        );
        
        // Aplicar ao noise ativo
        activeNoise.AmplitudeGain = currentNoiseAmplitude;
        activeNoise.FrequencyGain = currentNoiseFrequency;
        
        // Zerar o noise da câmera inativa
        if (activeNoise == defaultNoise && turboNoise != null)
        {
            turboNoise.AmplitudeGain = 0f;
            turboNoise.FrequencyGain = 0f;
        }
        else if (activeNoise == turboNoise && defaultNoise != null)
        {
            defaultNoise.AmplitudeGain = 0f;
            defaultNoise.FrequencyGain = 0f;
        }
    }
    
    private float CalculateSpeedNoiseIntensity(float speedKmh)
    {
        if (speedKmh < minSpeedForNoise) return 0f;
        
        float normalizedSpeed = Mathf.Clamp01((speedKmh - minSpeedForNoise) / (maxSpeedForNoise - minSpeedForNoise));
        return speedNoiseCurve.Evaluate(normalizedSpeed);
    }
    
    private float CalculateAccelerationNoiseIntensity(float acceleration)
    {
        // Converter aceleração para valor positivo normalizado
        float absAcceleration = Mathf.Abs(acceleration);
        float normalizedAcceleration = Mathf.Clamp01(absAcceleration / 20f); // 20 m/s² como máximo
        
        return accelerationNoiseCurve.Evaluate(normalizedAcceleration);
    }
    
    private float ApplyStateModifiers(float baseIntensity)
    {
        float modifiedIntensity = baseIntensity;
        
        // Modificar baseado no estado do carro
        if (carPhysics.IsTurboActive())
        {
            modifiedIntensity *= turboNoiseMultiplier; // Reduz noise durante turbo
        }
        
        if (carPhysics.IsDrifting())
        {
            modifiedIntensity *= driftNoiseMultiplier; // Aumenta noise durante drift
        }
        
        // Aumentar noise quando estiver no ar (efeito de instabilidade)
        if (!carPhysics.IsGrounded)
        {
            modifiedIntensity *= 1.3f;
        }
        
        return Mathf.Clamp01(modifiedIntensity);
    }
    
    private void CalculateTargetNoiseValues(CarType carType, float intensity, out float amplitude, out float frequency)
    {
        // Valores base por tipo de carro
        float maxAmplitude = (carType == CarType.classic) ? classicMaxAmplitude : hoverMaxAmplitude;
        float maxFrequency = (carType == CarType.classic) ? classicMaxFrequency : hoverMaxFrequency;
        
        // Aplicar intensidade
        amplitude = maxAmplitude * intensity;
        frequency = maxFrequency * intensity;
        
        // Adicionar variação baseada na aceleração (para sentir a estrada)
        if (accelerationNoiseIntensity > 0.1f)
        {
            amplitude += accelerationNoiseIntensity * 0.1f;
            frequency += accelerationNoiseIntensity * 0.2f;
        }
    }

    void OnDestroy()
    {
        StopAllCoroutines();
    }
}

public enum ShakePriority
{
    Low = 0,        // velocidade, estrada
    Medium = 1,     // drift, curvas
    High = 2,       // turbo, salto
    Critical = 3    // colisão, impacto forte
}