using UnityEngine;
using Unity.Cinemachine;
using System.Collections.Generic;

public class DefaultCamSwitcher : MonoBehaviour
{
    public List<CinemachineCamera> defaultCameras;
    public float changeInterval = 4f;
    private float timer;
    private int currentIndex;

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= changeInterval)
        {
            timer = 0;
            // Reseta a prioridade da câmera anterior
            defaultCameras[currentIndex].Priority.Value = 11;
            
            // Escolhe a próxima
            currentIndex = (currentIndex + 1) % defaultCameras.Count;
            
            // Aumenta a prioridade da nova (mas abaixo de 20, que é a prioridade de detecção)
            defaultCameras[currentIndex].Priority.Value = 15;
        }
    }
}