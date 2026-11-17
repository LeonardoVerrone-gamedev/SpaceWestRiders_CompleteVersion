
using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class SongManager : MonoBehaviour
{
    public static SongManager Instance { get; private set; }

    [Header("Configurações de Áudio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private List<AudioClip> musicList = new List<AudioClip>();

    [Header("Transição do Loop")]
    [SerializeField] private float fadeOutDuration = 2f;
    [SerializeField] private float silenceDuration = 2f;
    [SerializeField] private float fadeInDuration = 2f;

    private Coroutine transitionCoroutine;

    private float originalVolume;
    private bool isLooping;

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

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();

            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        originalVolume = audioSource.volume;
        audioSource.loop = false;
    }

    private void Update()
    {
        if (isLooping &&
            audioSource != null &&
            audioSource.clip != null &&
            audioSource.isPlaying &&
            transitionCoroutine == null)
        {
            float remainingTime =
                audioSource.clip.length - audioSource.time;

            if (remainingTime <= fadeOutDuration)
            {
                transitionCoroutine = StartCoroutine(
                    LoopTransition()
                );
            }
        }
    }

    public void PlaySong(int index, bool loop)
    {
        if (index < 0 || index >= musicList.Count)
        {
            StopSong();
            return;
        }

        AudioClip clipToPlay = musicList[index];

        if (clipToPlay == null)
        {
            Debug.LogWarning(
                $"[SongManager] O AudioClip no índice {index} é nulo."
            );
            return;
        }

        // Mesma música: apenas atualiza o estado de loop
        if (audioSource.clip == clipToPlay)
        {
            isLooping = loop;

            if (transitionCoroutine != null)
            {
                StopCoroutine(transitionCoroutine);
                transitionCoroutine = null;
                audioSource.volume = originalVolume;

                if (!audioSource.isPlaying)
                {
                    audioSource.Play();
                }
            }

            return;
        }

        // Cancela qualquer transição anterior
        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
            transitionCoroutine = null;
        }

        isLooping = loop;

        // Se já existe uma música tocando, faz a transição
        if (audioSource.isPlaying && audioSource.clip != null)
        {
            transitionCoroutine = StartCoroutine(
                ChangeSongTransition(clipToPlay)
            );
        }
        else
        {
            // Primeira música ou AudioSource parado
            audioSource.Stop();
            audioSource.clip = clipToPlay;
            audioSource.volume = originalVolume;
            audioSource.loop = false;
            audioSource.Play();
        }
    }

    private IEnumerator LoopTransition()
    {
        // FADE OUT
        yield return StartCoroutine(
            FadeVolume(audioSource.volume, 0f, fadeOutDuration)
        );

        audioSource.volume = 0f;
        audioSource.Stop();

        // SILÊNCIO
        if (silenceDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(silenceDuration);
        }

        // REINICIA A MÚSICA
        audioSource.time = 0f;
        audioSource.Play();

        // FADE IN
        yield return StartCoroutine(
            FadeVolume(0f, originalVolume, fadeInDuration)
        );

        audioSource.volume = originalVolume;
        transitionCoroutine = null;
    }

    private IEnumerator ChangeSongTransition(AudioClip newClip)
    {
        // Valores de transição pela metade
        float transitionFadeOut = fadeOutDuration / 2f;
        float transitionSilence = silenceDuration / 2f;
        float transitionFadeIn = fadeInDuration / 2f;

        // 1. FADE OUT
        yield return StartCoroutine(
            FadeVolume(audioSource.volume, 0f, transitionFadeOut)
        );

        audioSource.volume = 0f;
        audioSource.Stop();

        // 2. SILÊNCIO
        if (transitionSilence > 0f)
        {
            yield return new WaitForSecondsRealtime(transitionSilence);
        }

        // 3. TROCA DE MÚSICA
        audioSource.clip = newClip;
        audioSource.time = 0f;
        audioSource.Play();

        // 4. FADE IN
        yield return StartCoroutine(
            FadeVolume(0f, originalVolume, transitionFadeIn)
        );

        audioSource.volume = originalVolume;
        transitionCoroutine = null;
    }

    private IEnumerator FadeVolume(float startVolume, float targetVolume, float duration)
    {
        if (duration <= 0f)
        {
            audioSource.volume = targetVolume;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / duration);

            audioSource.volume = Mathf.Lerp(
                startVolume,
                targetVolume,
                t
            );

            yield return null;
        }

        audioSource.volume = targetVolume;
    }

    public void StopSong()
    {
        isLooping = false;

        if (transitionCoroutine != null)
        {
            StopCoroutine(transitionCoroutine);
            transitionCoroutine = null;
        }

        if (audioSource != null)
        {
            audioSource.Stop();
            audioSource.volume = originalVolume;
        }
    }
}
