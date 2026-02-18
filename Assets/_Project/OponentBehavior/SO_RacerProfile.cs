using UnityEngine;

[CreateAssetMenu(fileName = "NewRacerProfile", menuName = "Racing/AI Oponent Profile")]
public class SO_AIOponentProfile : ScriptableObject
{
    [Range(0, 1)] public float aggressiveness = 0.5f; // Frequência de ultrapassagem
    [Range(0, 1)] public float caution = 0.5f;       // Distância mantida de muros/carros
    [Range(0, 1)] public float defensiveSkill = 0.5f; // Capacidade de "fechar a porta"
    [Range(0, 1)] public float skillLevel = 0.7f; // 1.0 é um piloto perfeito, 0.2 é um iniciante

    [Range(0,1)] public float brutality = 0.7f;
}