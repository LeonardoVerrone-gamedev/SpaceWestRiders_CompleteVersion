using UnityEngine;

public class CircuitSelection : MonoBehaviour
{
    public CircuitSO[] allFreeCircuits;

    public void RandomPlay()
    {
        if(QuickPlayManagement.Instance != null)
        {
            QuickPlayManagement.Instance.CreateCircuit(null);
        }
    }

    public void PlayCircuit(string circuitID)
    {
      if(QuickPlayManagement.Instance != null)
        {
            QuickPlayManagement.Instance.CreateCircuit(circuitID);
        }  
    }
}