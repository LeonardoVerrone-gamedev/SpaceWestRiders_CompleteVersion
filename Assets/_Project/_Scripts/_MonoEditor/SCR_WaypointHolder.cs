using UnityEngine;
using System.Collections.Generic;

public class SCR_WaypointHolder : MonoBehaviour
{
    [Header("Configurações Visuais")]
    [SerializeField] private Color gizmoColor = Color.cyan;
    [SerializeField] private float gizmoRadius = 2f;

    // Esta é a lista que a IA vai consultar
    public List<Transform> waypoints = new List<Transform>();

    [ContextMenu("Atualizar Lista de Waypoints")]
    public void FetchWaypoints()
    {
        waypoints.Clear();

        // Pega todos os filhos diretos em ordem de hierarquia
        foreach (Transform child in transform)
        {
            waypoints.Add(child);
        }

        Debug.Log($"Sucesso: {waypoints.Count} waypoints encontrados em {gameObject.name}");
    }

    // Desenha as linhas no Editor para você visualizar o traçado
    private void OnDrawGizmos()
    {
        if (transform.childCount < 2) return;

        Gizmos.color = gizmoColor;
        
        for (int i = 0; i < transform.childCount; i++)
        {
            Vector3 current = transform.GetChild(i).position;
            Vector3 next;

            if (i < transform.childCount - 1)
            {
                next = transform.GetChild(i + 1).position;
            }
            else
            {
                // Fecha o circuito voltando para o primeiro
                next = transform.GetChild(0).position;
            }

            Gizmos.DrawSphere(current, gizmoRadius);
            Gizmos.DrawLine(current, next);
        }
    }
}