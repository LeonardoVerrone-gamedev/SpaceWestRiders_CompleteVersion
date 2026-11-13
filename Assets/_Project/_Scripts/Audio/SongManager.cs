using UnityEngine;
using System.Collections.Generic;

public class SongManager : MonoBehaviour
{
    public static SongManager Instance { get; private set; }

    [Header("Configurações de Áudio")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private List<AudioClip> musicList = new List<AudioClip>();

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

        // Garante que o AudioSource está referenciado
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
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
            Debug.LogWarning($"[SongManager] O AudioClip no índice {index} é nulo.");
            return;
        }

        // Se a mesma música já estiver tocando com o mesmo estado de loop, evita reiniciar
        if (audioSource.clip == clipToPlay && audioSource.isPlaying)
        {
            audioSource.loop = loop;
            return;
        }

        audioSource.Stop();
        audioSource.clip = clipToPlay;
        audioSource.loop = loop;
        audioSource.Play();
    }

    public void StopSong()
    {
        if (audioSource != null)
        {
            audioSource.Stop();
        }
    }
}