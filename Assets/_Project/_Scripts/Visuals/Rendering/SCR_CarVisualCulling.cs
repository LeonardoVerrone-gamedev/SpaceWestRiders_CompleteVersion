using UnityEngine;
using Unity.Cinemachine;
using System.Collections.Generic;

public class SCR_CarVisualCulling : MonoBehaviour
{
    [SerializeField] private SCR_RayBasedCarPhysics carPhysics;
    [SerializeField] private float viewportMargin = 0.3f;

    [SerializeField] private List<CinemachineBrain> brains = new List<CinemachineBrain>();
    [SerializeField] MeshRenderer[] meshes;

    void Awake()
    {
        if (carPhysics == null)
            carPhysics = GetComponent<SCR_RayBasedCarPhysics>();
    }

    void Start()
    {
        meshes = GetComponentsInChildren<MeshRenderer>(true);
    }

    void LateUpdate()
    {
        bool visible = IsVisibleByAnyBrain();
        carPhysics.SetVisualState(visible);

        foreach(MeshRenderer mesh in meshes) mesh.enabled = IsVisibleByAnyBrain();
    }

    public void AddCamera(CinemachineBrain brain)
    {
        brains.Add(brain);
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

        if(!carPhysics.AIControlled) return true; //se eh player entao é sempre visivel

        return false;
    }
}
