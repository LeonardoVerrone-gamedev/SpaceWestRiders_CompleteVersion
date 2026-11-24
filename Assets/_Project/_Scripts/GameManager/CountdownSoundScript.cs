using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class CountdownAudioController : MonoBehaviour
{
    public static CountdownAudioController Instance { get; private set; }

    [SerializeField] private AudioClip countdownClip;

    private AudioSource audioSource;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    public void Play()
    {
        if (countdownClip != null && audioSource != null)
            audioSource.PlayOneShot(countdownClip);
    }

    public void Stop()
    {
        if (audioSource != null)
            audioSource.Stop();
    }
}