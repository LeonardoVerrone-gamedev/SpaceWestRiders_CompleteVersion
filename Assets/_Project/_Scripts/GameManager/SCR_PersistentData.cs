using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using UnityEngine.InputSystem.Users;

public class SCR_PersistentData : MonoBehaviour {
    public static SCR_PersistentData Instance;
    public List<PlayerSessionData> players;
    public bool isSequenceRace = false; // Se for torneio, isso fica true

    void Awake() {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else Destroy(gameObject);
    }

    public void ResetSession()
    {
        // Despareia devices corretamente
        foreach (var p in players)
        {
            if (p.device != null)
            {
                var user = InputUser.FindUserPairedToDevice(p.device);
                if (user.HasValue && user.Value.valid)
                {
                    user.Value.UnpairDevices();
                }

            }
        }

        players.Clear();

        isSequenceRace = false;
    }

    public void FullReset()
    {
        ResetSession();
        Destroy(gameObject);
    }

}

[System.Serializable]
public class PlayerSessionData {
    public int playerIndex;
    public InputDevice device;
    public int selectedCarGridIndex = -1; // -1 significa "ainda não escolheu"
    public bool hasConfirmed;
    public RacerProfileSO selectedCarData;
}