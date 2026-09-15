using UnityEngine;

public class SCR_ImpactEffect : MonoBehaviour
{
    [SerializeField] ParticleSystem[] particles;
    [SerializeField] float effectDuration;

    private GameObject originalOwner;
    
    private float timer;
    public bool IsActive => gameObject.activeSelf;

    public float TimeRemaining => timer;

    public void Play(Vector3 position, Quaternion rotation, GameObject owner, bool setParentToNull = true)
    {

        if(originalOwner == null)
        {
            originalOwner = owner;
        }

        if(setParentToNull) transform.SetParent(null);

        transform.position = position;
        transform.rotation = rotation;
        gameObject.SetActive(true);
        
        CancelInvoke("Disable");
        foreach(ParticleSystem p in particles)
        {
            p.Clear(); // Limpa rastros anteriores
            p.Play();
        }

        timer = effectDuration;
        Invoke("Disable", effectDuration);
    }

    public void PlayOnPlace()
    {
        foreach(ParticleSystem p in particles)
        {
            p.Clear(); // Limpa rastros anteriores
            p.Play();
        }
    }

    void Update()
    {
        if (IsActive) timer -= Time.deltaTime;
    }

    void Disable()
    {
        this.transform.SetParent(originalOwner.transform);
        this.gameObject.SetActive(false);
    }
}