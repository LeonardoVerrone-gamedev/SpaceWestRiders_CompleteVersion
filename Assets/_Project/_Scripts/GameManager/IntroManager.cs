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

    [Header("Aviso de Pular Intro")]
    [SerializeField] private Font skipPromptFont;
    [SerializeField, Min(8)] private int skipPromptFontSize = 18;
    [SerializeField] private Color skipPromptColor = Color.white;
    [SerializeField, Min(0f)] private float skipPromptMarginX = 24f;
    [SerializeField, Min(0f)] private float skipPromptMarginY = 20f;

    private static bool hasPlayedIntro = false;
    private Coroutine currentIntroRoutine;
    private bool isIntroPlaying = false;

    private const string SkipPromptText = "aperte esc ou start para pular";
    private GUIStyle skipPromptStyle;

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

    private void EnsureSkipPromptStyle()
    {
        if (skipPromptStyle == null)
        {
            skipPromptStyle = new GUIStyle(GUI.skin.label);
        }

        if (skipPromptFont != null)
        {
            skipPromptStyle.font = skipPromptFont;
        }

        skipPromptStyle.fontSize = skipPromptFontSize;
        skipPromptStyle.alignment = TextAnchor.LowerRight;
        skipPromptStyle.normal.textColor = skipPromptColor;
        skipPromptStyle.wordWrap = false;
    }

    private void OnGUI()
    {
        if (!isIntroPlaying) return;

        EnsureSkipPromptStyle();

        GUIContent content = new GUIContent(SkipPromptText);
        Vector2 size = skipPromptStyle.CalcSize(content);

        // Canto inferior direito fixo em pixels independente da resolução da tela.
        float x = Screen.width - size.x - skipPromptMarginX;
        float y = Screen.height - size.y - skipPromptMarginY;

        Rect shadowRect = new Rect(x + 1.5f, y + 1.5f, size.x, size.y);
        Color prevColor = skipPromptStyle.normal.textColor;
        skipPromptStyle.normal.textColor = new Color(0f, 0f, 0f, 0.75f);
        GUI.Label(shadowRect, content, skipPromptStyle);

        skipPromptStyle.normal.textColor = prevColor;
        Rect labelRect = new Rect(x, y, size.x, size.y);
        GUI.Label(labelRect, content, skipPromptStyle);
    }

    private void Update()
    {
        if (!isIntroPlaying) return;

        bool keyboardEscape = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
        bool gamepadStart = ApertouStartEmAlgumGamepad();

        if (keyboardEscape || gamepadStart)
        {
            StopIntroAndFadeOut();
        }
    }

    private bool ApertouStartEmAlgumGamepad()
    {
        foreach (Gamepad gamepad in Gamepad.all)
        {
            if (gamepad != null && gamepad.startButton.wasPressedThisFrame)
            {
                return true;
            }
        }
        return false;
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