using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using UnityEngine.InputSystem.Users;
using UnityEngine.SceneManagement;

public class SCR_PersistentData : MonoBehaviour {
    public static SCR_PersistentData Instance;
    public List<PlayerSessionData> players;
    public bool isSequenceRace = false; // Se for torneio, isso fica true

    void Awake() {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else Destroy(gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

     private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "TitleScreen")
        {
            players?.Clear();
            Destroy(gameObject);
        }
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
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
public class PlayerSessionData
{
    public int playerIndex;
    public InputDevice device;
    public int selectedCarGridIndex; // Mantido para compatibilidade
    public string selectedCharacterID; // NOVO: para buscas robustas
    public RacerProfileSO selectedCarData;
    public bool hasConfirmed;
    
    public PlayerSessionData()
    {
        selectedCharacterID = "";
        selectedCarGridIndex = 0;
    }
}

[System.Serializable]
public class PlayerSessionSaveData
{
    public int playerIndex;
    public int selectedCarGridIndex;
    public string selectedCharacterID;
    public bool hasConfirmed;
}