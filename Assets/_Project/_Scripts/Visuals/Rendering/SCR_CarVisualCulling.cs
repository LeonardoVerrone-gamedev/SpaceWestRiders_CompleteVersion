using UnityEngine;
using Unity.Cinemachine;

public class SCR_CarVisualCulling : MonoBehaviour
{
    [SerializeField] private SCR_RayBasedCarPhysics carPhysics;
    [SerializeField] private float viewportMargin = 0.3f;

    private CinemachineBrain[] brains;

    void Awake()
    {
        if (carPhysics == null)
            carPhysics = GetComponent<SCR_RayBasedCarPhysics>();
    }

    void LateUpdate()
    {
        // Atualiza dinamicamente (spawn/despawn seguro)
        if (brains == null || brains.Length == 0)
            brains = FindObjectsOfType<CinemachineBrain>(false);

        bool visible = IsVisibleByAnyBrain();
        carPhysics.SetVisualState(visible);
    }

    private bool IsVisibleByAnyBrain()
    {
        Vector3 pos = transform.position;

        foreach (var brain in brains)
        {
            if (!brain || !brain.enabled) continue;

            Camera cam = brain.GetComponent<Camera>();
            if (!cam || !cam.enabled) continue;

            Vector3 vp = cam.WorldToViewportPoint(pos);

            if (vp.z < 0) continue;

            if (vp.x > -viewportMargin && vp.x < 1f + viewportMargin &&
                vp.y > -viewportMargin && vp.y < 1f + viewportMargin)
            {
                return true;
            }
        }

        return false;
    }
}
