using UnityEngine;
using Unity.Cinemachine;
using System.Collections;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;

public class CameraController : MonoBehaviour
{
    [SerializeField] bool useCinemachine = true;
    [SerializeField] CinemachineCamera vCam;
    [SerializeField] CinemachineCamera turbo_VCam;

    [SerializeField] Camera cam;

    private Transform player;
    private Rigidbody playerRB;

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
    }

    void Update()
    {
        // 1. Checagem de segurança e atualização de efeitos baseados em física
        HandlePhysicsBasedEffects();
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
        }
        else
        {
            carPhysics = null;
        }

        if (useCinemachine && vCam != null)
        {
            vCam.Follow = player;
            if (turbo_VCam != null) turbo_VCam.Follow = player;
            
            ResetAllCameras();
            vCam.Priority = 10;
            currentCameraType = CameraType.Default;
            UpdateVolumes(currentCameraType);
        }
    }

    public void SetChannel(int playerIndex)
    {
        // Converte o index (0, 1) para uma Layer Mask de canais (1, 2, 4...)
        // Canal 0 = 1, Canal 1 = 2, Canal 2 = 4
        OutputChannels channelMask = (OutputChannels)(1 << playerIndex);

        // 1. Configura o Brain para ouvir apenas esse canal
        var brain = GetComponentInChildren<CinemachineBrain>();
        if (brain != null)
        {
            brain.ChannelMask = channelMask;
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
            float newDistance = Mathf.SmoothDamp(currentDistance, targetDistance, ref currentDistanceVelocity, distanceSmoothTime);
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

    public CameraType GetCurrentCameraType() => currentCameraType;
    public bool IsTransitioning() => transitionLocked;


    void OnDestroy()
    {
        StopAllCoroutines();
    }
}