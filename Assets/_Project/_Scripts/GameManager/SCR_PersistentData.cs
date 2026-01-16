using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public class SCR_PersistentData : MonoBehaviour {
    public static SCR_PersistentData Instance;
    public List<PlayerSessionData> players = new List<PlayerSessionData>();
    public bool isSequenceRace = false; // Se for torneio, isso fica true

    void Awake() {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else Destroy(gameObject);
    }
}

[System.Serializable]
public class PlayerSessionData {
    public int playerIndex;
    public InputDevice device;
    public int selectedCarGridIndex = -1; // -1 significa "ainda não escolheu"
    public bool hasConfirmed;
    public SO_CharacterInfo selectedCarData;
}