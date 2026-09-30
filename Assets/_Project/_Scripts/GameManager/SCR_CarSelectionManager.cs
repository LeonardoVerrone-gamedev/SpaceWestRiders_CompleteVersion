using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;
using System.Collections.Generic;
using System.Linq;
using System;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;
using TMPro;

public class SCR_TrackSelectionManager : MonoBehaviour
{
    [Header("Story Mode Settings")]
    [SerializeField] private bool _isStoryMode = false;
    [SerializeField] private string _storyPlayer1ID;
    [SerializeField] private string _storyPlayer2ID;

    [Header("Selection Restrictions")]
    [SerializeField] private List<string> _blockedCharacterIDs = new List<string>();

    [SerializeField] private List<RacerProfileSO> _allCharactersSO; // A lista global de SOs
    private List<SCR_CarIdentity> _carsInScene;
    private Dictionary<string, SCR_CarInput> _carsByID; // NOVO: Mapa ID -> Carro

    [Header("Input Actions")]
    [SerializeField] private InputActionAsset _playerInputActions;

    private Dictionary<int, float> _playerJoinTimes = new Dictionary<int, float>();

    [Header("Grid Setup")]
    [SerializeField] private List<SCR_CarInput> _gridCars;
    CameraController camManager;
    
    [Header("UI & State")]
    private bool _inSelectionMode = true;
    private float _nextInputAllowedTime = 0f;
    private const float INPUT_COOLDOWN = 0.2f;

    [Header("Camera Management")]
    [SerializeField] private GameObject cameraPrefab;
    private List<CameraController> _activeCameras = new List<CameraController>();
    private List<SCR_CarVisualCulling> carCullings = new List<SCR_CarVisualCulling>();

    public static event Action OnRaceSetupCompleted;
    

    [Header("Race Start Settings")]
    [SerializeField] private float _waitForOthersTime = 3f;
    private Coroutine _startRaceCoroutine;
    private bool _isCountingDown = false;

    void Awake()
    {
        // Eliminação no Awake para modos de torneio
        if (GameManagerInstance.Instance != null)
        {
            switch (GameManagerInstance.Instance.currentGameMode)
            {
                case GameMode.MiniTournament:
                    if (MiniTournamentManager.Instance != null && 
                        MiniTournamentManager.Instance.CurrentState != null)
                    {
                        EliminateCarsFromEliminatedTeams();
                    }
                    break;
                case GameMode.Tournament:
                    if (FullTournamentManager.Instance != null && 
                        FullTournamentManager.Instance.CurrentState != null)
                    {
                        EliminateCarsFromTournamentFull();
                    }
                break;
                case GameMode.StoryMode:
                    if (FullTournamentManager.Instance != null && 
                        FullTournamentManager.Instance.CurrentState != null)
                    {
                        EliminateCarsFromTournamentFull();
                    }
                break;
            }
        }
    }

    bool HasPlayerWithoutDevice()
    {
        return SCR_PersistentData.Instance.players.Any(p => p.device == null);
    }

    void Start()
    {

        if (SCR_PersistentData.Instance != null)
        {
            SCR_PersistentData.Instance.ClearRuntimeDevices();
        }

        camManager = UnityEngine.Object.FindFirstObjectByType<CameraController>();

        _isStoryMode = GameManagerInstance.Instance.currentGameMode == GameMode.StoryMode;

        if(_isStoryMode)
        {
            _storyPlayer1ID = StoryModeManager.Instance.storyTournaments[StoryModeManager.Instance.CurrentState.currentTournamentIndex].storyTeam.racers[0].characterID;
            _storyPlayer2ID = StoryModeManager.Instance.storyTournaments[StoryModeManager.Instance.CurrentState.currentTournamentIndex].storyTeam.racers[1].characterID;
        
            int savedPlayers = SCR_PersistentData.Instance.players.Count;

            if(HasPlayerWithoutDevice())
            {
                PrepareSelectionNormally();
                return;
            }

            if(savedPlayers < 2)
            {
                StartRaceWithJoinWindow();
            }
            else
            {
                StartRaceImmediate();
            }
        }

        if (SCR_PersistentData.Instance != null 
            && SCR_PersistentData.Instance.isSequenceRace 
            && !_isStoryMode)
        {
            int savedPlayers = SCR_PersistentData.Instance.players.Count;

            if(savedPlayers > 0){
                if(SCR_PersistentData.Instance.players[0].device == null || SCR_PersistentData.Instance.players[savedPlayers - 1].device == null)
                { 
                    PrepareSelectionNormally(); 
                    return;
                }
            }

            if (savedPlayers >= 2)
            {
                StartRaceImmediate();
            }
            else if (savedPlayers == 1)
            {
                StartRaceWithJoinWindow();
            }
            else
            {
                PrepareSelectionNormally();
            }
        }
        else
        {
            PrepareSelectionNormally();
        }
    }

    void Update()
    {
        if (!_inSelectionMode) return;

        // Registro de novos players
        // Registro ou sequestro de device
        if (Keyboard.current.anyKey.wasPressedThisFrame)
        {
            if (SCR_PersistentData.Instance.players.Any(p => p.device == null) && !(SCR_PersistentData.Instance.players.Any(p => p.device == Keyboard.current)))
                AssignDeviceToLoadedPlayer(Keyboard.current);
            else
                RegisterPlayer(Keyboard.current);
        }

        foreach (var gamepad in Gamepad.all)
        {
            if (gamepad.allControls.Any(c =>
                c is UnityEngine.InputSystem.Controls.ButtonControl b &&
                b.wasPressedThisFrame))
            {
                if (SCR_PersistentData.Instance.players.Any(p => p.device == null) && !SCR_PersistentData.Instance.players.Any(p => p.device == gamepad))
                    AssignDeviceToLoadedPlayer(gamepad);
                else
                    RegisterPlayer(gamepad);
            }
        }

        HandleSelectionNavigation();
    }

    private void AssignDeviceToLoadedPlayer(InputDevice device)
    {
        if (SCR_PersistentData.Instance == null) return;

        if (SCR_CameramanAI.Instance != null)
        {
            SCR_CameramanAI.Instance.killCameraman();
        }

        // Já existe alguém usando esse device?
        if (SCR_PersistentData.Instance.players.Any(p => p.device == device))
            return;

        // Pega primeiro player sem device
        PlayerSessionData player =
            SCR_PersistentData.Instance.players
            .FirstOrDefault(p => p.device == null);

        if (player == null) return;

        player.device = device;

        BuildCarsDictionary();

        carCullings = UnityEngine.Object
            .FindObjectsByType<SCR_CarVisualCulling>(FindObjectsSortMode.None)
            .ToList();

        if (!_carsByID.ContainsKey(player.selectedCharacterID))
        {
            Debug.LogError($"Carro não encontrado para player {player.playerIndex}");
            return;
        }

        SCR_CarInput car = _carsByID[player.selectedCharacterID];

        // === CRIA CAMERA ===
        GameObject camObj = Instantiate(cameraPrefab);
        CameraController cam = camObj.GetComponent<CameraController>();

        // === TARGET ===
        cam.SetTarget(car.transform);

        cam.SetChannel(player.playerIndex);
        _activeCameras.Add(cam);

        int totalPlayers = SCR_PersistentData.Instance.players.Count;

        if (totalPlayers == 1)
        {
            cam.EnableSplitScreen(false, true);
            if (camObj.TryGetComponent<AudioListener>(out var listener))
                listener.enabled = true;
        }
        else if (totalPlayers == 2)
        {
            bool isP1 = player.playerIndex == 0;
            cam.EnableSplitScreen(true, isP1);
            if (camObj.TryGetComponent<AudioListener>(out var listener))
                listener.enabled = isP1;
        }

        // === CULLING ===
        var brain = camObj.GetComponentInChildren<Camera>()
            .GetComponent<CinemachineBrain>();

        foreach (var culling in carCullings)
            culling.AddCamera(brain);

        // Atualiza grid index
        player.selectedCarGridIndex =
            _gridCars.FindIndex(c => c.gameObject == car.gameObject);

        ConfirmSelection(player);

        Debug.Log($"Device {device.displayName} atribuído ao player {player.playerIndex}");
    }

    // Elimina carros de times eliminados
    private void EliminateCarsFromEliminatedTeams()
    {
        if (MiniTournamentManager.Instance?.CurrentState == null) return;

        int currentRaceIndex = MiniTournamentManager.Instance.CurrentState.currentRaceIndex;
        
        // Pega times eliminados ATÉ a corrida ANTERIOR
        var teamsEliminatedBeforeCurrent = MiniTournamentManager.Instance.CurrentState.eliminationHistory
            .GetTeamsEliminatedByRace(currentRaceIndex - 1);
        
        var allCars = UnityEngine.Object.FindObjectsByType<SCR_CarIdentity>(FindObjectsSortMode.None);
        
        foreach (var car in allCars)
        {
            if (car.racerData != null && car.racerData.team != null)
            {
                if (teamsEliminatedBeforeCurrent.Contains(car.racerData.team))
                {
                    Destroy(car.gameObject);
                }
            }
        }
    }

    private void EliminateCarsFromTournamentFull()
    {
        if (FullTournamentManager.Instance?.CurrentState == null) return;

        int currentRaceIndex = FullTournamentManager.Instance.CurrentState.currentRaceIndex;
        
        // Pega times eliminados ATÉ a corrida ANTERIOR
        var teamsEliminatedBeforeCurrent = FullTournamentManager.Instance.CurrentState.eliminationHistory
            .GetTeamsEliminatedByRace(currentRaceIndex - 1);
        
        var allCars = UnityEngine.Object.FindObjectsByType<SCR_CarIdentity>(FindObjectsSortMode.None);
        
        foreach (var car in allCars)
        {
            if (car.racerData != null && car.racerData.team != null)
            {
                if (teamsEliminatedBeforeCurrent.Contains(car.racerData.team))
                {
                    Destroy(car.gameObject);
                }
            }
        }
    }

    // Constrói dicionário de carros por ID
    private void BuildCarsDictionary()
    {
        _carsByID = new Dictionary<string, SCR_CarInput>();
        _carsInScene = UnityEngine.Object.FindObjectsByType<SCR_CarIdentity>(FindObjectsSortMode.None).ToList();
        
        foreach (var car in _carsInScene)
        {
            if (car.racerData != null && !string.IsNullOrEmpty(car.racerData.characterID))
            {
                if (!_carsByID.ContainsKey(car.racerData.characterID))
                {
                    _carsByID[car.racerData.characterID] = car.GetComponent<SCR_CarInput>();
                }
            }
        }
    }

    private void PrepareSelectionNormally()
    {
        if (SCR_PersistentData.Instance == null)
            new GameObject("PersistentData").AddComponent<SCR_PersistentData>();

        _allCharactersSO = _allCharactersSO.OrderBy(so => so.characterID).ToList();

        BuildCarsDictionary();

        carCullings = UnityEngine.Object
            .FindObjectsByType<SCR_CarVisualCulling>(FindObjectsSortMode.None)
            .ToList();

        PrepareCarsForSelection();

        if(!_isStoryMode)
            SCR_PersistentData.Instance.isSequenceRace = true;
    }

    private void StartRaceWithJoinWindow()
    {
        if (_startRaceCoroutine != null)
            return;
        
        _inSelectionMode = true;

        CarSelectionManagerScript.Instance.ShowP2JoinInstruction();

        BuildCarsDictionary();

        carCullings = UnityEngine.Object
            .FindObjectsByType<SCR_CarVisualCulling>(FindObjectsSortMode.None)
            .ToList();

        var p = SCR_PersistentData.Instance.players[0];

        // Verifica se o carro ainda existe
        if (!_carsByID.ContainsKey(p.selectedCharacterID))
        {
            Debug.LogError($"Jogador {p.playerIndex} está eliminado! Não é possível iniciar corrida.");
            return;
        }

        GameObject camObj = Instantiate(cameraPrefab);
        CameraController newCamController = camObj.GetComponent<CameraController>();
        newCamController.SetTarget(_carsByID[p.selectedCharacterID].transform);
        newCamController.SetChannel(p.playerIndex);
        newCamController.EnableSplitScreen(false, true);

        _activeCameras.Add(newCamController);

        var brain = camObj.GetComponentInChildren<Camera>().GetComponent<CinemachineBrain>();
        foreach (var carCulling in carCullings)
            carCulling.AddCamera(brain);

        // Usa o dicionário para encontrar o carro

        p.hasConfirmed = true;
        _isCountingDown = false;

        _startRaceCoroutine = StartCoroutine(WaitToStartRaceRoutine());
    }

    private void PrepareCarsForSelection()
    {
        foreach (var car in _gridCars)
        {
            if (car != null && car.TryGetComponent<Rigidbody>(out var rb)) 
                rb.isKinematic = true;
        }
    }

    private void RegisterPlayer(InputDevice device)
    {
        if (SCR_PersistentData.Instance.players.Any(p => p.device == device)) return;

        if (_startRaceCoroutine != null)
        {
            StopCoroutine(_startRaceCoroutine);
            _isCountingDown = false;
        }

        int newIndex = SCR_PersistentData.Instance.players.Count;

        if(newIndex == 0)
        {
            if (SCR_CameramanAI.Instance != null)
            {
                SCR_CameramanAI.Instance.killCameraman();
            }

            CarSelectionManagerScript.Instance.ShowSelectionHud(true, false);
        }

        PlayerSessionData newPlayer = new PlayerSessionData
        {
            playerIndex = newIndex,
            device = device,
            selectedCarGridIndex = 0,
            selectedCharacterID = "",
            selectedCarData = _allCharactersSO[0],
            hasConfirmed = _isStoryMode
        };

        if (_isStoryMode)
        {
            string targetID = (newIndex == 0) ? _storyPlayer1ID : _storyPlayer2ID;
            string targetIDStr = targetID;
            newPlayer.selectedCharacterID = targetIDStr;
            newPlayer.selectedCarData = _allCharactersSO.FirstOrDefault(c => c.characterID == targetIDStr);
            
            newPlayer.selectedCarGridIndex = _gridCars.FindIndex(car => 
                car != null && car.GetComponent<SCR_CarIdentity>() != null && 
                car.GetComponent<SCR_CarIdentity>().racerData.characterID == targetIDStr);
        }
        else
        {
            RacerProfileSO selectedSO = null;
            int selectedIndex = 0;
            
            for (int i = 0; i < _allCharactersSO.Count; i++)
            {
                string id = _allCharactersSO[i].characterID;
                bool isBlocked = _blockedCharacterIDs.Contains(id);
                bool isTaken = SCR_PersistentData.Instance.players.Any(p => p.selectedCharacterID == _allCharactersSO[i].characterID);

                if (!isBlocked && !isTaken)
                {
                    selectedSO = _allCharactersSO[i];
                    selectedIndex = i;
                    break;
                }
            }
            
            if (selectedSO != null)
            {
                newPlayer.selectedCharacterID = selectedSO.characterID;
                newPlayer.selectedCarData = selectedSO;
                newPlayer.selectedCarGridIndex = selectedIndex;
            }
            else
            {
                newPlayer.selectedCharacterID = _allCharactersSO[0].characterID;
                newPlayer.selectedCarData = _allCharactersSO[0];
                newPlayer.selectedCarGridIndex = 0;
            }
        }

        SCR_PersistentData.Instance.players.Add(newPlayer);
        _playerJoinTimes[newIndex] = Time.time;

        // === ATUALIZA HUD PARA MULTIPLAYER ===
        // === ATUALIZA HUD DA SELEÇÃO ===
        if (SCR_PersistentData.Instance.players.Count == 1)
        {
            CarSelectionManagerScript.Instance.ShowSelectionHud(true, false);
        }
        else if (SCR_PersistentData.Instance.players.Count >= 2)
        {
            CarSelectionManagerScript.Instance.ShowSelectionHud(true, true);
        }

        BuildCarsDictionary();

        GameObject camObj = Instantiate(cameraPrefab);
        CameraController newCamController = camObj.GetComponent<CameraController>();

        foreach(SCR_CarVisualCulling carCulling in carCullings)
        {
            carCulling.AddCamera(camObj.GetComponentInChildren<Camera>().GetComponent<CinemachineBrain>());
        }
        
        newCamController.SetChannel(newIndex);
        _activeCameras.Add(newCamController);

        UpdatePlayerCamera(newPlayer);

        if (SCR_PersistentData.Instance.players.Count == 1)
        {
            newCamController.EnableSplitScreen(false, true);
            if (camObj.TryGetComponent<AudioListener>(out var listener)) listener.enabled = true;
        }
        else if (SCR_PersistentData.Instance.players.Count == 2)
        {
            _activeCameras[0].EnableSplitScreen(true, true);
            _activeCameras[1].EnableSplitScreen(true, false);
            if (camObj.TryGetComponent<AudioListener>(out var listener)) listener.enabled = false;
        }

        if (_isStoryMode) CheckAllReady();
    }

    private void HandleSelectionNavigation()
    {
        if (_isStoryMode) return;

        if (Time.time < _nextInputAllowedTime) return;

        foreach (var p in SCR_PersistentData.Instance.players)
        {
            if (p.hasConfirmed) continue;

            int direction = 0;
            bool confirmed = false;

            bool canConfirm = _playerJoinTimes.ContainsKey(p.playerIndex) && 
                         (Time.time - _playerJoinTimes[p.playerIndex] > 0.15f);

            if (p.device is Keyboard k)
            {
                if (k.dKey.wasPressedThisFrame || k.rightArrowKey.wasPressedThisFrame) direction = 1;
                if (k.aKey.wasPressedThisFrame || k.leftArrowKey.wasPressedThisFrame) direction = -1;
                if (canConfirm && (k.enterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame)) 
                confirmed = true;
            }
            else if (p.device is Gamepad g)
            {
                if (g.leftStick.x.ReadValue() > 0.5f || g.dpad.right.wasPressedThisFrame) direction = 1;
                if (g.leftStick.x.ReadValue() < -0.5f || g.dpad.left.wasPressedThisFrame) direction = -1;
                if (canConfirm && g.buttonSouth.wasPressedThisFrame) 
                confirmed = true;
            }

            if (direction != 0)
            {
                MoveSelection(p, direction);
                _nextInputAllowedTime = Time.time + INPUT_COOLDOWN;
            }

            if (confirmed) ConfirmSelection(p);
        }
    }

    private void MoveSelection(PlayerSessionData p, int direction)
    {
        int currentIndex = -1;
        for (int i = 0; i < _allCharactersSO.Count; i++)
        {
            if (_allCharactersSO[i].characterID == p.selectedCharacterID)
            {
                currentIndex = i;
                break;
            }
        }
        
        if (currentIndex == -1) currentIndex = 0;
        
        int max = _allCharactersSO.Count;
        int nextIndex = (currentIndex + direction + max) % max;

        bool isValid = false;
        int safetyBreak = 0;

        while (!isValid && safetyBreak < max)
        {
            string targetID = _allCharactersSO[nextIndex].characterID;
            bool isBlocked = _blockedCharacterIDs.Contains(targetID);
            bool isTaken = SCR_PersistentData.Instance.players.Any(other => 
                other != p && other.selectedCharacterID == targetID);
            bool carExists = _carsByID != null && _carsByID.ContainsKey(targetID);

            if (isBlocked || isTaken || !carExists)
            {
                nextIndex = (nextIndex + direction + max) % max;
                safetyBreak++;
            }
            else
            {
                isValid = true;

                // === ANIMAÇÃO DA SETA ===
                bool isSingleplayer =
                    SCR_PersistentData.Instance.players.Count == 1;

                bool isP1 = p.playerIndex == 0;

                bool isRight = direction > 0;

                CarSelectionManagerScript.Instance.OnSideSelectionPressed(
                    isSingleplayer,
                    isP1,
                    isRight
                );
            }
        }

        p.selectedCharacterID = _allCharactersSO[nextIndex].characterID;
        p.selectedCarData = _allCharactersSO[nextIndex];
        p.selectedCarGridIndex = nextIndex;

        UpdatePlayerCamera(p);
    }

    private void UpdatePlayerCamera(PlayerSessionData p)
    {
        if (_carsByID != null &&
            _carsByID.ContainsKey(p.selectedCharacterID) &&
            p.playerIndex < _activeCameras.Count)
        {
            var cam = _activeCameras[p.playerIndex];

            cam.SetTarget(_carsByID[p.selectedCharacterID].transform);

            cam.EnableSplitScreen(
                SCR_PersistentData.Instance.players.Count > 1,
                p.playerIndex == 0
            );
        }
    }

    private void ConfirmSelection(PlayerSessionData p)
    {
        p.hasConfirmed = true;

        CarSelectionManagerScript.Instance.HidePlayerSelectionHud(p.playerIndex == 1);

        if (SCR_PersistentData.Instance.players.Count == 1 &&
        p.playerIndex == 0)
        {
            CarSelectionManagerScript.Instance.Show3SecondsCountdown();
        }

        CheckAllReady();
    }

    private void CheckAllReady()
    {
        int playersCount = SCR_PersistentData.Instance.players.Count;
        bool allConfirmed = SCR_PersistentData.Instance.players.All(p => p.hasConfirmed);

        if (allConfirmed)
        {
            if (playersCount >= 2)
            {
                if (_startRaceCoroutine != null) StopCoroutine(_startRaceCoroutine);
                FinalizeSetupAndStartRace();
            }
            else if (!_isCountingDown)
            {
                _startRaceCoroutine = StartCoroutine(WaitToStartRaceRoutine());
            }
        }
    }

    private System.Collections.IEnumerator WaitToStartRaceRoutine()
    {
        _isCountingDown = true;
        float timer = _waitForOthersTime;

        Debug.Log($"P1 pronto. Esperando {timer}s por outros jogadores...");

        while (timer > 0)
        {
            if (SCR_PersistentData.Instance.players.Count > 1)
            {
                Debug.Log("Novo player detectado! Cancelando contagem.");
                _isCountingDown = false;
                yield break;
            }

            timer -= Time.deltaTime;
            yield return null;
        }

        FinalizeSetupAndStartRace();
    }

    private void FinalizeSetupAndStartRace()
    {
        _inSelectionMode = false;

        CarSelectionManagerScript.Instance.ShowSelectionHud(false, false);

        BuildCarsDictionary();

        if (SCR_CameramanAI.Instance != null)
        {
            SCR_CameramanAI.Instance.killCameraman();
        }

        // Remove da grid os carros que não existem mais
        var carsToRemove = new List<SCR_CarInput>();
        foreach (var car in _gridCars)
        {
            if (car == null || !_carsInScene.Any(c => c.gameObject == car.gameObject))
            {
                carsToRemove.Add(car);
            }
        }
        foreach (var car in carsToRemove)
        {
            if (_gridCars.Contains(car))
                _gridCars.Remove(car);
        }

        for (int i = 0; i < _gridCars.Count; i++)
        {
            SCR_CarInput car = _gridCars[i];
            if (car == null) continue;
            
            var identity = car.GetComponent<SCR_CarIdentity>();
            if (identity == null || identity.racerData == null) continue;
            
            string carID = identity.racerData.characterID;
            PlayerSessionData owner = SCR_PersistentData.Instance.players
                .FirstOrDefault(p => p.selectedCharacterID == carID);

            if (owner != null)
            {

                if(owner.device is Gamepad)
                {
                    car.gameObject.GetComponent<SCR_GamepadVibrationController>().enabled = true;
                }

                // Remove AI
                if (car.TryGetComponent<AIRacingController>(out var ai))
                    Destroy(ai);

                // Define modo lógico do carro
                InputMode mode = owner.device is Keyboard ? InputMode.Keyboard : InputMode.Gamepad;
                car.SetInputMode(mode);

                // ===== RESET TOTAL DO INPUT =====
                if (car.TryGetComponent<PlayerInput>(out var oldPI))
                    Destroy(oldPI);

                var newPI = car.gameObject.AddComponent<PlayerInput>();
                newPI.neverAutoSwitchControlSchemes = true;
                newPI.actions = _playerInputActions;
                newPI.defaultActionMap = "Driving";
                newPI.defaultControlScheme = owner.device is Keyboard ? "Keyboard&Mouse" : "Gamepad";
                newPI.enabled = false;

                car.RefreshInputActions();
                StartCoroutine(EnablePlayerInputNextFrame(newPI, owner.device, car));
            }
            else
            {
                if (car.TryGetComponent<PlayerInput>(out var pInput))
                    Destroy(pInput);

                car.SetInputMode(InputMode.AI_Controlled);
                if (car.TryGetComponent<AIRacingController>(out var ai)) ai.enabled = true;
            }

            if (car.TryGetComponent<PlayerInput>(out var PI))
            {
                PI.enabled = false;
                PI.enabled = true;
            }

            if (car.TryGetComponent<Rigidbody>(out var rb))
            {
                RaceManager.Instance.AddRigidbodyForRaceStart(rb);
            }
            car.enabled = true;
        }

        OnRaceSetupCompleted.Invoke();
        Debug.Log("TODOS PRONTOS! 3... 2... 1... GO!");
    }

    private System.Collections.IEnumerator EnablePlayerInputNextFrame(PlayerInput pi, InputDevice device, SCR_CarInput car)
    {
        yield return null;

        if (pi == null) yield break;

        pi.enabled = true;
        pi.user.UnpairDevices();
        InputUser.PerformPairingWithDevice(device, pi.user);
        pi.ActivateInput();
        car.enabled = true;
    }

    private void StartRaceImmediate()
    {
        _inSelectionMode = false;

        BuildCarsDictionary();

        carCullings = UnityEngine.Object
            .FindObjectsByType<SCR_CarVisualCulling>(FindObjectsSortMode.None)
            .ToList();

        // Remove da grid os carros que não existem mais
        var carsToRemove = new List<SCR_CarInput>();
        foreach (var car in _gridCars)
        {
            if (car == null || !_carsInScene.Any(c => c.gameObject == car.gameObject))
            {
                carsToRemove.Add(car);
            }
        }
        foreach (var car in carsToRemove)
        {
            if (_gridCars.Contains(car))
                _gridCars.Remove(car);
        }

        // Recria as câmeras para os jogadores que já estão no PersistentData
        foreach (var p in SCR_PersistentData.Instance.players)
        {
            GameObject camObj = Instantiate(cameraPrefab);
            CameraController newCamController = camObj.GetComponent<CameraController>();
            
            newCamController.SetChannel(p.playerIndex);
            _activeCameras.Add(newCamController);

            if (_carsByID.ContainsKey(p.selectedCharacterID))
            {
                newCamController.SetTarget(_carsByID[p.selectedCharacterID].transform);
                
                // ATUALIZA o selectedCarGridIndex para a nova cena
                p.selectedCarGridIndex = _gridCars.FindIndex(car => car.gameObject == _carsByID[p.selectedCharacterID].gameObject);
            }

            int totalPlayers = SCR_PersistentData.Instance.players.Count;
            if (totalPlayers == 1)
            {
                newCamController.EnableSplitScreen(false, true);
                if (camObj.TryGetComponent<AudioListener>(out var listener)) listener.enabled = true;
            }
            else if (totalPlayers == 2)
            {
                bool isP1 = p.playerIndex == 0;
                newCamController.EnableSplitScreen(true, isP1);
                if (camObj.TryGetComponent<AudioListener>(out var listener)) listener.enabled = isP1;
            }

            var brain = camObj.GetComponentInChildren<Camera>().GetComponent<CinemachineBrain>();
            foreach (var carCulling in carCullings)
            {
                carCulling.AddCamera(brain);
            }
        }

        FinalizeSetupAndStartRace();
    }
}