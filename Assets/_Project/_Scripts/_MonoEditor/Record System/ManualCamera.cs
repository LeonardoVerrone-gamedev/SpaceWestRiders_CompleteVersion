using UnityEngine;
using Unity.Cinemachine; // Se estiver no Unity 6 / Cinemachine 3.0. Use 'using Cinemachine;' para versões antigas.

public class ManualTrackCam : MonoBehaviour
{
    [Header("Detecção")]
    public float detectionRadius = 80f;
    public float fovThreshold = 50f;
    public LayerMask obstructionMask;

    [Header("Suavização")]
    public float lookSmoothness = 8f;

    private CinemachineCamera vcam; 
    private Transform currentTarget;
    private Quaternion initialRot;
    private int basePriority;

    void Start()
    {
        vcam = GetComponent<CinemachineCamera>();
        initialRot = transform.rotation;
        // Salva a prioridade que você definiu no Inspector (ex: 10)
        basePriority = (int)vcam.Priority.Value;
    }

    void Update()
    {
        FindBestCar();

        if (currentTarget != null)
        {
            // Aumenta a prioridade para o Brain cortar para esta câmera
            vcam.Priority.Value = 20; 
            
            // Rotação manual via script (o seu controle total)
            Vector3 dir = (currentTarget.position - transform.position).normalized;
            Quaternion targetRot = Quaternion.LookRotation(dir);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * lookSmoothness);
        }
        else
        {
            // Reseta para a prioridade original para o Brain escolher outra câmera
            vcam.Priority.Value = basePriority;
            transform.rotation = Quaternion.Slerp(transform.rotation, initialRot, Time.deltaTime * 2f);
        }
    }

    void FindBestCar()
    {
        // Busca qualquer carro com seu script de física
        SCR_RayBasedCarPhysics[] cars = Object.FindObjectsByType<SCR_RayBasedCarPhysics>(FindObjectsSortMode.None);
        float closest = float.MaxValue;
        Transform best = null;

        foreach (var car in cars)
        {
            float dist = Vector3.Distance(transform.position, car.transform.position);
            if (dist < detectionRadius)
            {
                Vector3 dir = (car.transform.position - transform.position).normalized;
                if (Vector3.Angle(transform.forward, dir) < fovThreshold)
                {
                    // Raycast para garantir que não há paredes no caminho
                    if (!Physics.Linecast(transform.position, car.transform.position, obstructionMask))
                    {
                        if (dist < closest) { closest = dist; best = car.transform; }
                    }
                }
            }
        }
        currentTarget = best;
    }
}