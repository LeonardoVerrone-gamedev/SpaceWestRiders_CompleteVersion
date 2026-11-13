using UnityEngine;

public class SongTrigger : MonoBehaviour
{
    [Header("Configuração do Gatilho")]
    [SerializeField] private int songIndex;
    [SerializeField] private bool loopSong = true;
    [SerializeField] private bool triggerOnAwake = true;

    private void Awake()
    {
        if (triggerOnAwake)
        {
            PlayAssignedSong();
        }
    }

    public void PlayAssignedSong()
    {
        if (SongManager.Instance != null)
        {
            SongManager.Instance.PlaySong(songIndex, loopSong);
        }
        else
        {
            Debug.LogWarning($"[SongTrigger] Nenhum SongManager encontrado na cena ao tentar tocar a música {songIndex}.");
        }
    }
}