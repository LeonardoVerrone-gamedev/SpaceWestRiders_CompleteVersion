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
    [SerializeField] private int _storyPlayer1ID; // ID do Protagonista
    [SerializeField] private int _storyPlayer2ID;

    [Header("Selection Restrictions")]
    [SerializeField] private List<int> _blockedCharacterIDs = new List<int>();

    [SerializeField] private List<RacerProfileSO> _allCharactersSO; // A lista global de SOs
    private List<SCR_CarIdentity> _carsInScene;

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

    void Start()
    {
        camManager = UnityEngine.Object.FindFirstObjectByType<CameraController>();

        if (SCR_PersistentData.Instance != null && SCR_PersistentData.Instance.isSequenceRace && !_isStoryMode)
        {
            StartRaceImmediate();
        }
        else
        {
            if (SCR_PersistentData.Instance == null)
                new GameObject("PersistentData").AddComponent<SCR_PersistentData>();
                
            _allCharactersSO = _allCharactersSO.OrderBy(so => so.characterID).ToList();

            _carsInScene = UnityEngine.Object.FindObjectsByType<SCR_CarIdentity>(FindObjectsSortMode.None).ToList();
            carCullings = UnityEngine.Object.FindObjectsByType<SCR_CarVisualCulling>(FindObjectsSortMode.None).ToList();
            PrepareCarsForSelection();

            if(!_isStoryMode) SCR_PersistentData.Instance.isSequenceRace = true; //adicionar if(!isStoryMode) se isso quebrar algo
        }
    }

    void Update()
    {
        if (!_inSelectionMode) return;

        // Registro de novos players
        if (Keyboard.current.anyKey.wasPressedThisFrame) RegisterPlayer(Keyboard.current);
        foreach (var gamepad in Gamepad.all)
        {
            if (gamepad.allControls.Any(c => c is UnityEngine.InputSystem.Controls.ButtonControl b && b.wasPressedThisFrame))
                RegisterPlayer(gamepad);
        }

        HandleSelectionNavigation();
    }

    private void PrepareCarsForSelection()
    {
        foreach (var car in _gridCars)
        {
            if (car.TryGetComponent<Rigidbody>(out var rb)) rb.isKinematic = true;
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

        PlayerSessionData newPlayer = new PlayerSessionData
        {
            playerIndex = newIndex,
            device = device,
            selectedCarGridIndex = 0,
            // Inicializa com o primeiro personagem disponível
            selectedCarData = _allCharactersSO[0],
            hasConfirmed = _isStoryMode
        };

        if (_isStoryMode)
        {
            // Assinala o personagem da história baseado na ordem de entrada
            int targetID = (newIndex == 0) ? _storyPlayer1ID : _storyPlayer2ID;
            newPlayer.selectedCarData = _allCharactersSO.FirstOrDefault(c => int.Parse(c.characterID) == targetID);
            
            // Sincroniza o index do grid para o FinalizeSetup encontrar o carro
            newPlayer.selectedCarGridIndex = _gridCars.FindIndex(car => 
                int.Parse(car.GetComponent<SCR_CarIdentity>().racerData.characterID) == targetID);
        }
        
        else
        {
            // Encontra o primeiro carro que não está bloqueado nem ocupado por outro player
            int firstValidIndex = 0;
            for (int i = 0; i < _allCharactersSO.Count; i++)
            {
                int id = int.Parse(_allCharactersSO[i].characterID);
                bool isBlocked = _blockedCharacterIDs.Contains(id);
                bool isTaken = SCR_PersistentData.Instance.players.Any(pl => pl.selectedCarGridIndex == i);

                if (!isBlocked && !isTaken)
                {
                    firstValidIndex = i;
                    break;
                }
            }
            newPlayer.selectedCarGridIndex = firstValidIndex;
            newPlayer.selectedCarData = _allCharactersSO[firstValidIndex];
        }

        SCR_PersistentData.Instance.players.Add(newPlayer);

        _playerJoinTimes[newIndex] = Time.time;

        GameObject camObj = Instantiate(cameraPrefab);
        CameraController newCamController = camObj.GetComponent<CameraController>();

        foreach(SCR_CarVisualCulling carCulling in carCullings)
        {
            carCulling.AddCamera(camObj.GetComponentInChildren<Camera>().GetComponent<CinemachineBrain>());
        }
        
        newCamController.SetChannel(newIndex);
        _activeCameras.Add(newCamController);

        // Posiciona a câmera no carro inicial
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

            // Leitura de Input simplificada para o exemplo
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
        int max = _allCharactersSO.Count;
        int nextIndex = (p.selectedCarGridIndex + direction + max) % max;

        // Loop de validação: continua pulando enquanto o carro for inválido
        bool isValid = false;
        int safetyBreak = 0; // Evita loop infinito se todos estiverem bloqueados

        while (!isValid && safetyBreak < max)
        {
            int targetID = int.Parse(_allCharactersSO[nextIndex].characterID);

            // Critério 1: Está na lista de bloqueados?
            bool isBlocked = _blockedCharacterIDs.Contains(targetID);

            // Critério 2: Outro player já pegou?
            bool isTaken = SCR_PersistentData.Instance.players.Any(other => 
                other != p && other.selectedCarGridIndex == nextIndex);

            if (isBlocked || isTaken)
            {
                nextIndex = (nextIndex + direction + max) % max;
                safetyBreak++;
            }
            else
            {
                isValid = true;
            }
        }

        p.selectedCarGridIndex = nextIndex;
        p.selectedCarData = _allCharactersSO[nextIndex];

        UpdatePlayerCamera(p);
    }

    private void UpdatePlayerCamera(PlayerSessionData p)
    {
        int targetID = int.Parse(_allCharactersSO[p.selectedCarGridIndex].characterID);
        var targetCar = _carsInScene.FirstOrDefault(c => c.racerData != null && int.Parse(c.racerData.characterID) == targetID);

        if (targetCar != null)
        {
            _activeCameras[p.playerIndex].SetTarget(targetCar.transform);
        }
    }

    private void ConfirmSelection(PlayerSessionData p)
    {
        p.hasConfirmed = true;
        CheckAllReady();
    }

    private void CheckAllReady()
    {
        int playersCount = SCR_PersistentData.Instance.players.Count;
        bool allConfirmed = SCR_PersistentData.Instance.players.All(p => p.hasConfirmed);

        if (allConfirmed)
        {
            // Se temos 2 players (máximo) e ambos confirmaram, larga na hora
            if (playersCount >= 2)
            {
                if (_startRaceCoroutine != null) StopCoroutine(_startRaceCoroutine);
                FinalizeSetupAndStartRace();
            }
            // Se só temos 1 player, inicia a contagem de espera por um segundo jogador
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
            // Se um novo player entrar durante a contagem, paramos tudo
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

        for (int i = 0; i < _gridCars.Count; i++)
        {
            SCR_CarInput car = _gridCars[i];
            PlayerSessionData owner = SCR_PersistentData.Instance.players.FirstOrDefault(p => p.selectedCarGridIndex == i);

            // 1. Configuração de Controle (Player vs IA)
           if (owner != null)
            {
                // Remove AI
                if (car.TryGetComponent<AIRacingController>(out var ai))
                    Destroy(ai);

                // Define modo lógico do carro
                InputMode mode = owner.device is Keyboard ? InputMode.Keyboard : InputMode.Gamepad;
                car.SetInputMode(mode);

                // ===== RESET TOTAL DO INPUT =====

                // Remove PlayerInput antigo se existir
                if (car.TryGetComponent<PlayerInput>(out var oldPI))
                    Destroy(oldPI);

                // Cria PlayerInput novo
                var newPI = car.gameObject.AddComponent<PlayerInput>();

                newPI.neverAutoSwitchControlSchemes = true;
                newPI.actions = _playerInputActions;
                newPI.defaultActionMap = "Driving";
                newPI.defaultControlScheme = owner.device is Keyboard
                    ? "Keyboard&Mouse"
                    : "Gamepad";

                // Começa DESATIVADO — OnEnable ainda não roda
                newPI.enabled = false;

                // Rebind no SCR_CarInput agora (pega refs)
                car.RefreshInputActions();

                // Ativa e faz pairing NO PRÓXIMO FRAME
                StartCoroutine(EnablePlayerInputNextFrame(newPI, owner.device, car));

            }
            else
            {
                if (car.TryGetComponent<PlayerInput>(out var pInput))
                {
                    Destroy(pInput);
                }

                car.SetInputMode(InputMode.AI_Controlled);
                if (car.TryGetComponent<AIRacingController>(out var ai)) ai.enabled = true;
            }

            if (car.TryGetComponent<PlayerInput>(out var PI))
            {
                PI.enabled = false;
                PI.enabled = true;
            }

            // 2. Liberação da Física (Movido para garantir que execute para todos)
            if (car.TryGetComponent<Rigidbody>(out var rb))
            {
                rb.isKinematic = false;
                rb.WakeUp(); // Garante que a física processe imediatamente
            }
            car.enabled = true;
        }
        OnRaceSetupCompleted.Invoke();
        Debug.Log("TODOS PRONTOS! 3... 2... 1... GO!");

        //Invoke("GoToNextSceneTest", 5f); testing :)
    }

    private System.Collections.IEnumerator EnablePlayerInputNextFrame(PlayerInput pi, InputDevice device, SCR_CarInput car)
    {
        yield return null; // frame seguinte → PlayerInput inicializa certo

        if (pi == null) yield break;

        pi.enabled = true; // OnEnable cria InputUser corretamente

        // agora o user é válido
        pi.user.UnpairDevices();
        InputUser.PerformPairingWithDevice(device, pi.user);

        pi.ActivateInput();

        car.enabled = true;
    }


    private void StartRaceImmediate()
    {
        _inSelectionMode = false;

        // 1. Localiza as referências necessárias na cena nova
        _carsInScene = UnityEngine.Object.FindObjectsByType<SCR_CarIdentity>(FindObjectsSortMode.None).ToList();
        
        // 2. Recria as câmeras para os jogadores que já estão no PersistentData
        foreach (var p in SCR_PersistentData.Instance.players)
        {
            // Instancia a câmera prefab
            GameObject camObj = Instantiate(cameraPrefab);
            CameraController newCamController = camObj.GetComponent<CameraController>();
            
            newCamController.SetChannel(p.playerIndex);
            _activeCameras.Add(newCamController);

            // Configura SplitScreen baseado no total de jogadores salvos
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

            // Foca a câmera no carro que o player escolheu (via characterID)
            var targetCar = _carsInScene.FirstOrDefault(c => c.racerData != null && c.racerData.characterID == p.selectedCarData.characterID);
            if (targetCar != null)
            {
                newCamController.SetTarget(targetCar.transform);
                
                // ATUALIZA o selectedCarGridIndex para a nova cena
                // Isso garante que seu loop no FinalizeSetup localize o 'owner' corretamente pelo índice
                p.selectedCarGridIndex = _gridCars.FindIndex(car => car.gameObject == targetCar.gameObject);
            }
        }

        // 3. Agora que as câmeras existem e os índices foram mapeados para a nova cena, inicia
        FinalizeSetupAndStartRace();
    }
}