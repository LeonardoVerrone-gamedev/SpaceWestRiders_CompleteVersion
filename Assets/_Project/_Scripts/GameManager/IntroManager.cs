using UnityEngine;
using UnityEngine.Video;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using System.Collections;

public class IntroManager : MonoBehaviour
{
    public static IntroManager Instance { get; private set; }

    [Header("Configurações da Intro")]
    [SerializeField] private string targetSceneName = "TitleScreen";
    [SerializeField] private bool alwaysPlay = false;
    [SerializeField] private VideoPlayer videoPlayer;
    [SerializeField] private Canvas introCanvas;
    [SerializeField] private CanvasGroup introCanvasGroup;
    [SerializeField] private float fadeOutDuration = 1.0f;

    private static bool hasPlayedIntro = false;
    private Coroutine currentIntroRoutine;
    private bool isIntroPlaying = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Update()
    {
        if (!isIntroPlaying) return;

        bool keyboardEscape = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        bool gamepadStart = Gamepad.current != null && Gamepad.current.startButton.wasPressedThisFrame;

        if (keyboardEscape || gamepadStart)
        {
            StopIntroAndFadeOut();
        }
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == targetSceneName)
        {
            if (!hasPlayedIntro || alwaysPlay)
            {
                if (currentIntroRoutine != null)
                {
                    StopCoroutine(currentIntroRoutine);
                }
                currentIntroRoutine = StartCoroutine(PlayIntroRoutine());
            }
            else
            {
                DisableIntroUI();
            }
        }
    }

    private IEnumerator PlayIntroRoutine()
    {
        hasPlayedIntro = true;
        isIntroPlaying = true;

        if (introCanvas == null || introCanvasGroup == null || videoPlayer == null)
        {
            isIntroPlaying = false;
            yield break;
        }

        introCanvas.sortingOrder = 9999;

        introCanvasGroup.alpha = 1f;
        introCanvasGroup.blocksRaycasts = true;
        introCanvasGroup.interactable = true;

        videoPlayer.Prepare();
        while (!videoPlayer.isPrepared)
        {
            yield return null;
        }

        videoPlayer.Play();

        while (!videoPlayer.isPlaying)
        {
            yield return null;
        }

        double videoLength = videoPlayer.length;
        if (videoLength <= 0) videoLength = 5.0f;

        float timer = 0f;
        while (timer < videoLength && videoPlayer.isPlaying && isIntroPlaying)
        {
            timer += Time.unscaledDeltaTime;
            yield return null;
        }

        yield return AntibugFadeOut();
    }

    private void StopIntroAndFadeOut()
    {
        if (!isIntroPlaying) return;

        if (currentIntroRoutine != null)
        {
            StopCoroutine(currentIntroRoutine);
        }

        StartCoroutine(AntibugFadeOut());
    }

    private IEnumerator AntibugFadeOut()
    {
        isIntroPlaying = false;

        if (videoPlayer != null && videoPlayer.isPlaying)
        {
            videoPlayer.Stop();
        }

        float fadeTimer = 0f;
        float startAlpha = introCanvasGroup != null ? introCanvasGroup.alpha : 1f;

        while (fadeTimer < fadeOutDuration)
        {
            fadeTimer += Time.unscaledDeltaTime;
            if (introCanvasGroup != null)
            {
                introCanvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, fadeTimer / fadeOutDuration);
            }
            yield return null;
        }

        DisableIntroUI();
    }

    private void DisableIntroUI()
    {
        isIntroPlaying = false;

        if (introCanvasGroup != null)
        {
            introCanvasGroup.alpha = 0f;
            introCanvasGroup.blocksRaycasts = false;
            introCanvasGroup.interactable = false;
        }

        if (videoPlayer != null && videoPlayer.isPlaying)
        {
            videoPlayer.Stop();
        }
    }
}