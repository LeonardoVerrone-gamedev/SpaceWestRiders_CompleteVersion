using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.EventSystems;

public class CarSelectionMenu : MonoBehaviour
{
    public RacerProfileSO[] racers;

    QuickPlayManagement quickPlayManager;

    public void SetManagement(QuickPlayManagement _quickPlay = null)
    {
        if(_quickPlay != null) quickPlayManager = _quickPlay;
    }

    void Update()
    {
        // Registro de novos players
        if (Keyboard.current.anyKey.wasPressedThisFrame) RegisterPlayer(Keyboard.current);
        foreach (var gamepad in Gamepad.all)
        {
            if (gamepad.allControls.Any(c => c is UnityEngine.InputSystem.Controls.ButtonControl b && b.wasPressedThisFrame))
                RegisterPlayer(gamepad);
        }
    }

    private void RegisterPlayer(InputDevice device)
    {
        if (SCR_PersistentData.Instance.players.Any(p => p.device == device)) return;

        int newIndex = SCR_PersistentData.Instance.players.Count;

        PlayerSessionData newPlayer = new PlayerSessionData
        {
            playerIndex = newIndex,
            device = device,
            selectedCarGridIndex = 0,
            // Inicializa com o primeiro personagem disponível
            selectedCarData = null,
            hasConfirmed = false
        };

        SCR_PersistentData.Instance.players.Add(newPlayer);
    }

    //button
    public void SelectCar(string racerID)
    {
        // 1. Identifica qual dispositivo disparou a ação (Gamepad ou Teclado)
        InputDevice dispositivoAtivo = null;

        if (Gamepad.current != null && Gamepad.current.allControls.Any(c => c.IsPressed()))
            dispositivoAtivo = Gamepad.current;
        else if (Keyboard.current != null && Keyboard.current.anyKey.isPressed)
            dispositivoAtivo = Keyboard.current;

        if (dispositivoAtivo == null) return;

        // 2. Busca o player correspondente no seu PersistentData usando o device
        var player = SCR_PersistentData.Instance.players
            .FirstOrDefault(p => p.device == dispositivoAtivo);

        if (player != null)
        {
            // 3. Busca o RacerProfileSO correspondente ao racerID (via LINQ)
            var racerData = racers.FirstOrDefault(r => r.characterID == racerID); 
            
            if (racerData != null)
            {
                player.selectedCarData = racerData;
                Debug.Log($"Player {player.playerIndex} selecionou: {racerData.name} usando {dispositivoAtivo.displayName}");
            }
        }
    }

    public void Finish()
    {
        if(QuickPlayManagement.Instance != null) QuickPlayManagement.Instance.StartRace();
    }
}