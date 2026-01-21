using UnityEngine;
using System.Collections.Generic;

public enum AIState { Racing, Overtaking, Defending, Recovering }

public class AIRacingController : MonoBehaviour
{
    [Header("Perfil do Piloto")]
    [SerializeField] private SO_RacerProfile profile;

    [Header("Targeting")]
    [SerializeField] private List<Transform> waypoints = new List<Transform>();
    [SerializeField] private int currentTargetIndex = 0;
    [SerializeField] private float waypointPassRadius = 30f;
    [SerializeField] private float lookAheadDistance = 20f;
    [SerializeField] private float lookAheadSpeedFactor = 1.5f; // Quanto maior, mais longe ela olha ao acelerar

    [Header("Rubber banding")]
    private bool huntsLeader = true;
    [SerializeField] float hoverTargetAccelerationBoost = 1.8f;
    [SerializeField] float classicTargetAccelerationBoost = 1.5f;
    [SerializeField] float waitAccelerationDecrease = 0.75f;

    [Header("Rubber Banding Settings")]
    [SerializeField] float maxCatchUpBoost = 1.5f; // 50% mais rápido se estiver longe atrás
    [SerializeField] float maxWaitSlowdown = 0.75f; // 75% mais lento se estiver longe à frente

    [Header("Sensores e Detecção")]
    [SerializeField] private Transform sensorPivot;
    [SerializeField] private LayerMask wallLayer;
    [SerializeField] private LayerMask opponentLayer;
    [SerializeField] private float opponentDetectDist = 80f;
    [SerializeField] private float sideSensorDist = 8f;      
    [SerializeField] private float diagonalSensorDist = 45f; 
    [SerializeField] private float frontSensorDist = 120f;    
    [SerializeField] private float avoidanceForce = 5.0f;
    [SerializeField] private float sideDecisionCooldown = 1.5f; // Tempo mínimo mantendo o mesmo lado

    [Header("Car Movement")]
    [SerializeField] private float maxSteerAngle = 15f; 
    [SerializeField] private float steeringSmoothness = 15f;

    [Header("Configurações de Erro")]
    [SerializeField] private float maxTracingError = 5.0f; // Metros máximos de desvio do traçado
    private float currentTracingNoise = 0f;
    private float noiseChangeTimer = 0f;

    [Header("Dificuldade")]
    [SerializeField] private AIDifficulty currentDifficulty = AIDifficulty.Hard;

    [Header("Turbo e drift")]
    [SerializeField] private float driftThresholdAngle = 25f; // Ângulo mínimo para decidir driftar
    [SerializeField] private float turboStraightReach = 150f; // Distância de reta livre para soltar turbo
    [SerializeField] private float minStaminaForTurbo = 20f;

    // Estado Interno
    private AIState currentState = AIState.Racing;
    private Rigidbody rb;
    private SCR_CarInput carInputs;
    private SCR_RayBasedCarPhysics car;
    private float stuckTimer = 0f;
    private bool isRecovering = false;
    private float lateralOffset = 0f; // Usado para ultrapassagem/defesa
    private float wrongWayTimer = 0f;
    private float lastSideDecisionTime;
    private float chosenSide = 0; // -1 (esquerda), 1 (direita)
    float recoveryGraceTimer;
    private float targetTracingNoise = 0f;

    private bool _isCurrentlyDrifting = false;
    float _lastSteerOutput;

    SCR_RayBasedCarPhysics rayBasedPhysics;

    void OnEnable()//switch to onEnable later
    {
        car = GetComponent<SCR_RayBasedCarPhysics>();
        rb = GetComponent<Rigidbody>();
        carInputs = GetComponent<SCR_CarInput>();
        rayBasedPhysics = GetComponent<SCR_RayBasedCarPhysics>();
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        if (sensorPivot == null) sensorPivot = transform;

        SetDifficulty(currentDifficulty);

        waypoints.Clear();
        SCR_WaypointHolder holder = FindFirstObjectByType<SCR_WaypointHolder>();

        if (holder != null)
        {
            // Chama o método para garantir que a lista está populada
            //holder.FetchWaypoints(); 
            
            // Retorna a lista exata
            waypoints = holder.waypoints;
        }
    }

    public void SetDifficulty(AIDifficulty difficulty)
    {
        currentDifficulty = difficulty;
        
        float accMult = 7f;
        float speedMult = 9f;
        float decelDivisor = 10f;

        switch (difficulty)
        {
            case AIDifficulty.Easy:
                // IA lenta, demora a acelerar e freia muito antes
                accMult = 3f;
                speedMult = 4f;
                decelDivisor = 1f;
                break;

            case AIDifficulty.Medium:
                // IA competitiva, mas comete erros de frenagem
                accMult = 5f;
                speedMult = 6.5f;
                decelDivisor = 1f;
                break;

            case AIDifficulty.Hard:
                // Seu padrão atual: trilhos e velocidade alta
                accMult = 7f;
                speedMult = 9f;
                decelDivisor = 1f;
                break;

            case AIDifficulty.UltraHard:
                // IA "F-Zero": aceleração insana e frenagem instantânea
                accMult = 10f;
                speedMult = 12f;
                decelDivisor = 1f;
                break;
        }

        if (rayBasedPhysics != null)
        {
            //rayBasedPhysics.SetAI(true, accMult, speedMult, decelDivisor, this);
            rayBasedPhysics.SetAI(true, 1f, 1f, 1f, this);
        }
    }

    void FixedUpdate()
    {
        if (waypoints == null || waypoints.Count == 0) return;

        if (isRecovering) 
        {
            // Forçamos os inputs a zero para o script de física não acelerar
            if (carInputs != null)
            {
                carInputs.SetThrottleInput(0f);
                carInputs.SetSteeringInput(0f);
                carInputs.SetHandbrakeInput(false);
            }
            return; 
        }

        CheckIfStuck();
        CheckDirection();
        UpdateTargetIndex();

        HandleAdvancedDriving();

        DetermineAIState();

        float steeringInput = (currentState == AIState.Recovering) ? CalculateRecoverySteer() : CalculateSteering();
        float throttleInput = (currentState == AIState.Recovering) ? -0.5f : CalculateThrottle();

        if (carInputs != null)
        {
            carInputs.SetSteeringInput(steeringInput);
            carInputs.SetThrottleInput(throttleInput);

            //carInputs.SetHandbrakeInput(shouldDrift); 

            if (currentState == AIState.Racing && car.IsGrounded)
            {
                // Calculamos a velocidade lateral, mas isolamos o plano horizontal
                Vector3 vel = rb.linearVelocity;
                Vector3 side = transform.right;
                side.y = 0; // Garante que a força lateral não empurre para cima/baixo
                side.Normalize();

                float sideVelMag = Vector3.Dot(vel, side);
                Vector3 sideVelocity = side * sideVelMag;
                
                float gripCorrection = Mathf.Lerp(2.0f, 0.5f, Mathf.Abs(carInputs.GetCurrentInputState().steering));
                
                // Aplicamos a força ignorando variações de inclinação bruscas
                //rb.AddForce(-sideVelocity * rb.mass * gripCorrection, ForceMode.Force);
            }

            // --- MELHORIA DE COMBATIVIDADE: FORÇA DE ARRANCADA ---
            // Se a IA está acelerando e no chão, damos um empurrão extra
            // Isso compensa a falta de "reflexo" da IA na saída de curvas
           // if (throttleInput > 0.1f)
           // {
               // float combatMultiplier = (currentState != AIState.Racing) ? 1.3f : 1.0f;
                // Aplica uma força direta proporcional à agressividade do perfil
               // rb.AddForce(transform.forward * (profile.aggressiveness * 4000f * combatMultiplier), ForceMode.Force);
            //}
        }
    }

    public void SetRecovering()
    {
        isRecovering = true;
        currentState = AIState.Recovering;

        // PARA TUDO: Zera velocidade linear e angular imediatamente
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        float recoveryTime = Random.Range(0.2f, 1f);

        // Opcional: Cancelar Invokes anteriores para garantir que o tempo de parada seja respeitado
        CancelInvoke("StopRecovery");
        Invoke("StopRecovery", recoveryTime); // Tempo que ele ficará parado/congelado
    }

    void DetermineAIState()
    {
        if (isRecovering) { currentState = AIState.Recovering; return; }

        RaycastHit hit;
        Vector3 fwd = sensorPivot.forward;
        Vector3 backDir = -sensorPivot.forward;
        float transitionSpeed = Mathf.Lerp(5f, 2f, rb.linearVelocity.magnitude / 150f);

        // --- 1. DEFESA COM CHANCE DE ERRO ---
        float defenseRange = opponentDetectDist * 0.6f;
        if (Physics.BoxCast(sensorPivot.position, new Vector3(2.5f, 2f, 1f), backDir, out hit, sensorPivot.rotation, defenseRange, opponentLayer))
        {
            if (hit.transform != transform && Random.value < profile.skillLevel && profile.defensiveSkill > 0.2f)
            {
                currentState = AIState.Defending;
                Vector3 opponentLocalPos = transform.InverseTransformPoint(hit.transform.position);
                
                if (Mathf.Abs(opponentLocalPos.x) > 2.0f)
                    chosenSide = opponentLocalPos.x > 0 ? 1f : -1f;

                ApplyLateralMovement(transitionSpeed);
                return;
            }
        }

        // --- 2. ULTRAPASSAGEM ---
        if (Physics.BoxCast(sensorPivot.position, new Vector3(2f, 2f, 2f), fwd, out hit, sensorPivot.rotation, opponentDetectDist, opponentLayer))
        {
            // 1. Pegamos a velocidade de avanço da própria IA
            float myForwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
            
            // 2. Pegamos a velocidade de avanço do oponente
            // Usamos o forward da IA também para saber se estamos chegando nele
            Rigidbody opponentRb = hit.rigidbody;
            float opponentForwardSpeed = (opponentRb != null) ? Vector3.Dot(opponentRb.linearVelocity, transform.forward) : 0;

            float relativeSpeedZ = myForwardSpeed - opponentForwardSpeed;

            if (hit.transform != transform && Random.value < profile.aggressiveness && profile.aggressiveness > 0.3f && (relativeSpeedZ > 2f || hit.distance < 15f))
            {
                Vector3 potentialSide = transform.InverseTransformPoint(hit.transform.position).x > 0 ? -transform.right : transform.right;

                if (!Physics.Raycast(sensorPivot.position, potentialSide, sideSensorDist, wallLayer))
                {
                    currentState = AIState.Overtaking;
                    
                    if (Time.time > lastSideDecisionTime + sideDecisionCooldown || chosenSide == 0)
                    {
                        chosenSide = transform.InverseTransformPoint(hit.transform.position).x > 0 ? -1f : 1f;
                        lastSideDecisionTime = Time.time;
                    }

                    ApplyLateralMovement(transitionSpeed);
                }
                else 
                {
                    // Se não tem espaço, ela fica em Racing (no vácuo) mas não tenta a manobra lateral
                    currentState = AIState.Racing;
                }
                return;
            }
        }

        // --- 3. RACING ---
        currentState = AIState.Racing;
        chosenSide = 0;
        lateralOffset = Mathf.MoveTowards(lateralOffset, 0, Time.fixedDeltaTime * (transitionSpeed * 0.5f));
    }

    // Método auxiliar para evitar repetição de código e checar paredes
    void ApplyLateralMovement(float transitionSpeed)
    {
        // 1. Definimos a direção e o alvo desejado
        float targetAmt = (currentState == AIState.Overtaking ? 6f : 4f) * chosenSide;
        Vector3 sideDir = transform.right * chosenSide;

        // 2. Sensores de segurança (Rays extras para "enxergar" a parede antes de virar)
        RaycastHit wallHit;
        bool wallAhead = Physics.Raycast(sensorPivot.position, (transform.forward + sideDir).normalized, out wallHit, sideSensorDist * 2f, wallLayer);
        bool wallSide = Physics.Raycast(sensorPivot.position, sideDir, out wallHit, sideSensorDist, wallLayer);

        // 3. Se houver parede no caminho do desvio, cancelamos a ultrapassagem lateral
        if (wallAhead || wallSide)
        {
            // Se bateríamos na parede, forçamos o offset de volta para 0 (centro da pista)
            lateralOffset = Mathf.MoveTowards(lateralOffset, 0, Time.fixedDeltaTime * 15f);
            
            // Opcional: Aborta o estado se o caminho estiver bloqueado por paredes
            // currentState = AIState.Racing; 
        }
        else
        {
            // Só aplica o desvio se o caminho estiver livre
            lateralOffset = Mathf.MoveTowards(lateralOffset, targetAmt, Time.fixedDeltaTime * transitionSpeed);
        }
    }

    float CalculateSteering()
    {
        noiseChangeTimer -= Time.fixedDeltaTime;
        if (noiseChangeTimer <= 0)
        {
            float effectiveSkill = Mathf.Clamp01(profile.skillLevel);
            float errorRange = (1f - effectiveSkill) * maxTracingError;
            
            // Em vez de mudar instantaneamente, definimos um novo alvo
            targetTracingNoise = Random.Range(-errorRange, errorRange);
            noiseChangeTimer = Random.Range(3f, 6f); // Intervalos um pouco maiores
        }

        // Interpola suavemente para o novo erro
        currentTracingNoise = Mathf.Lerp(currentTracingNoise, targetTracingNoise, Time.fixedDeltaTime * 2f);

        //float combatLookAheadBonus = (currentState == AIState.Overtaking || currentState == AIState.Defending) ? 1.2f : 1.0f;
        Vector3 targetPos = GetLookAheadPoint();

        // Se estiver em drift, aumentamos a sensibilidade do volante para corrigir a trajetória
        float driftMultiplier = _isCurrentlyDrifting ? 1.5f : 1.0f;
        
        float avoidance = GetDifferentialAvoidance();
        float wallDanger = Mathf.Abs(avoidance);
        float safetyFilter = Mathf.Clamp01(1.0f - (wallDanger * 1.5f));

        float targetLateralOffset = lateralOffset;
        if (currentState == AIState.Defending) {
            // Aumenta o offset se o oponente tentar passar, mas reduz se houver parede
            targetLateralOffset *= 1.2f; 
        }

        // --- CORREÇÃO: Erro relativo à PISTA, não ao CARRO ---
        int nextIndex = (currentTargetIndex + 1) % waypoints.Count;
        Vector3 trackDir = (waypoints[nextIndex].position - waypoints[currentTargetIndex].position).normalized;
        Vector3 trackRight = Vector3.Cross(Vector3.up, trackDir).normalized; // Direita real da pista

        float effectiveOffset = (targetLateralOffset + currentTracingNoise) * safetyFilter;

        // Agora o alvo fica parado em relação à pista, mesmo que o carro balance
        targetPos += trackRight * effectiveOffset;

        Vector3 directionToTarget = targetPos - transform.position;

        // Projeta o vetor no plano local do carro (ignorando a altura relativa ao loop)
        Vector3 localDir = transform.InverseTransformDirection(directionToTarget);
        localDir.y = 0; // "Achata" o alvo no horizonte do carro
        float angleToTarget = Vector3.SignedAngle(Vector3.forward, localDir.normalized, Vector3.up);
        
        // --- NOVO: DEADZONE E SENSIBILIDADE ---
        //float angleToTarget = Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg;

        float speedMS = rb.linearVelocity.z;

        float dynamicDeadzone = Mathf.Lerp(8.0f, 6.0f, speedMS / 20f);
        
        float rawSteerInput = 0f;
        if (Mathf.Abs(angleToTarget) > dynamicDeadzone)
        {
            rawSteerInput = angleToTarget / maxSteerAngle;
            // Curva de potência para suavizar o centro (mais controle)
            rawSteerInput = Mathf.Sign(rawSteerInput) * Mathf.Pow(Mathf.Abs(rawSteerInput), 1.5f) * driftMultiplier;
        }

        float finalTarget = Mathf.Clamp(rawSteerInput + avoidance, -1f, 1f);

        // --- CORREÇÃO 4: Damping Interno Dinâmico ---
        // Se estiver em linha reta, o volante volta pro centro muito rápido.
        // Se estiver virando, ele é mais suave.
        float steerLerpSpeed = (Mathf.Abs(finalTarget) < 0.1f) ? 15f : 8f;
        
        _lastSteerOutput = Mathf.MoveTowards(_lastSteerOutput, finalTarget, Time.fixedDeltaTime * steerLerpSpeed);

        return _lastSteerOutput;
    }

    Vector3 GetLookAheadPoint()
    {
        // Use a velocidade local Z para o cálculo, evita oscilações de inclinação
        float forwardSpeed = rb.linearVelocity.z;
        float dynamicLookAhead = lookAheadDistance + (Mathf.Max(0, forwardSpeed) * lookAheadSpeedFactor);
        
        Vector3 currentWp = waypoints[currentTargetIndex].position;
        int nextIndex = (currentTargetIndex + 1) % waypoints.Count;
        Vector3 nextWp = waypoints[nextIndex].position;

        Vector3 segmentDir = (nextWp - currentWp).normalized;
        float segmentLength = Vector3.Distance(currentWp, nextWp);
        
        // Projetamos a posição do carro no segmento para saber onde ele está na linha
        float dot = Vector3.Dot(transform.position - currentWp, segmentDir);
        
        // O LookAhead deve ser calculado a partir da nossa PROJEÇÃO na pista, 
        // não da nossa posição física atual (evita o zigue-zague se o carro sair da linha)
        Vector3 carProjectedOnTrack = currentWp + segmentDir * Mathf.Clamp(dot, 0, segmentLength);
        
        float targetProgress = Mathf.Clamp(dot, 0, segmentLength) + dynamicLookAhead;

        if (targetProgress > segmentLength)
        {
            int afterNextIndex = (nextIndex + 1) % waypoints.Count;
            Vector3 secondSegmentDir = (waypoints[afterNextIndex].position - nextWp).normalized;
            return nextWp + secondSegmentDir * (targetProgress - segmentLength);
        }
        
        return currentWp + (segmentDir * targetProgress);
    }

    private void SetNearestWaypointAsTarget()
    {
        float closestDistance = Mathf.Infinity;
        int closestIndex = 0;

        for (int i = 0; i < waypoints.Count; i++)
        {
            float distance = Vector3.Distance(transform.position, waypoints[i].position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestIndex = i;
            }
        }

        // Define o alvo como o próximo após o mais próximo, 
        // para garantir que ela não tente voltar no caminho.
        currentTargetIndex = (closestIndex + 1) % waypoints.Count;
        Debug.Log($"AI Recovery: Redirecionando para waypoint {currentTargetIndex}");
    }

    float CalculateThrottle()
    {

        if (isRecovering)
        {
            // Se estivermos apontando quase para o lado oposto, damos um pouco de ré 
            // e depois aceleramos tudo enquanto viramos o volante
            float dot = Vector3.Dot(transform.forward, (waypoints[currentTargetIndex].position - transform.position).normalized);
            return dot < 0 ? -0.4f : 1f; 
        }

        float pitch = transform.eulerAngles.x;
        if (pitch > 45f && pitch < 315f) // Detecta que o carro está inclinado/em loop
        {
            return 1.0f; // Força aceleração máxima para não cair
        }

        float currentSpeedMS = rb.linearVelocity.magnitude;
        float targetSpeedMS = (profile.baseTargetSpeedKmh / 3.6f) + Random.Range(-10f, 30f);

        // Frenagem por Curvatura
        Vector3 localTarget = transform.InverseTransformPoint(waypoints[currentTargetIndex].position);
        float angle = Mathf.Abs(Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg);
        targetSpeedMS *= Mathf.Clamp01(1.0f - (angle / 80f));

        // Cautela: Reduz velocidade se houver carros muito próximos
        if (currentState == AIState.Overtaking) targetSpeedMS *= (1.1f); // "Drafting/Pushing"
        
        
        float baseThrottle = currentSpeedMS < targetSpeedMS ? 1f : -0.3f;
        float rubberFactor = ApplyRubberBanding(baseThrottle);

        if (currentState == AIState.Overtaking) 
        {
            // Se estiver muito perto do oponente (vácuo/drafting), não freia tanto na curva
            targetSpeedMS *= 1.15f; 
            // Garante aceleração máxima
            baseThrottle = 1.0f; 
        }

        if (currentState == AIState.Defending)
        {
            // Na defesa, a IA "ocupa mais espaço" se for levemente mais lenta no meio da curva
            targetSpeedMS *= 0.95f; 
        }

        return rubberFactor;
    }

    float GetDifferentialAvoidance()
    {
        float correction = 0;
        Vector3 fwd = sensorPivot.forward;
        Vector3 rgt = sensorPivot.right;
        correction += GetAvoidanceBalance(rgt, -rgt, sideSensorDist, 1.0f);
        Vector3 diagR = (fwd + rgt).normalized;
        Vector3 diagL = (fwd - rgt).normalized;
        correction += GetAvoidanceBalance(diagR, diagL, diagonalSensorDist, 2.0f);
        Vector3 frontR = (fwd * 3.5f + rgt).normalized;
        Vector3 frontL = (fwd * 3.5f - rgt).normalized;
        correction += GetAvoidanceBalance(frontR, frontL, frontSensorDist, 4.0f);
        return correction * avoidanceForce * 0.1f;
    }

    float GetAvoidanceBalance(Vector3 dirR, Vector3 dirL, float length, float weight)
    {
        RaycastHit hitR, hitL;
        // Filtra para ignorar o próprio carro usando o LayerMask e conferindo o transform
        float forceR = Physics.Raycast(sensorPivot.position, dirR, out hitR, length, wallLayer) ? (1f - (hitR.distance / length)) : 0;
        float forceL = Physics.Raycast(sensorPivot.position, dirL, out hitL, length, wallLayer) ? (1f - (hitL.distance / length)) : 0;
        return (forceL - forceR) * weight;
    }

    void UpdateTargetIndex()
    {
        float dynamicRadius = waypointPassRadius + (rb.linearVelocity.magnitude * 0.8f);
        if (Vector3.Distance(transform.position, waypoints[currentTargetIndex].position) < dynamicRadius)
            currentTargetIndex = (currentTargetIndex + 1) % waypoints.Count;
    }

    void CheckIfStuck()
    {
        if (isRecovering) return;
    
        // Timer de carência após sair de uma recuperação
        if (recoveryGraceTimer > 0)
        {
            recoveryGraceTimer -= Time.fixedDeltaTime;
            return;
        }

        if (rb.linearVelocity.magnitude < 2f) // Aumentei para 2f para ser mais sensível
        {
            float stuckThreshold = Random.Range(0.15f, 1f);
            stuckTimer += Time.fixedDeltaTime;
            if (stuckTimer > stuckThreshold) 
            { 
                SetRecovering(); // Use o método que limpa as forças
            }
        }
        else stuckTimer = 0f;
    }

    void CheckDirection()
    {
        if (isRecovering) return; // Não checa se já estiver recuperando

        if (Mathf.Abs(transform.up.y) < 0.5f) return;

        Vector3 wpPos = waypoints[currentTargetIndex].position;
        int prevIndex = currentTargetIndex == 0 ? waypoints.Count - 1 : currentTargetIndex - 1;
        Vector3 trackDirection = (wpPos - waypoints[prevIndex].position).normalized;
        
        float dot = Vector3.Dot(transform.forward, trackDirection);

        // Se estiver na contramão (dot negativo) e se movendo
        if (dot < -0.2f && rb.linearVelocity.magnitude > 2f)
        {
            wrongWayTimer += Time.fixedDeltaTime;
            if (wrongWayTimer > 1.2f) 
            {
                isRecovering = true;
                wrongWayTimer = 0f; // Reseta o timer
                if(!IsInvoking("StopRecovery")) Invoke("StopRecovery", Random.Range(0.25f, 1.5f));
            }
        }
        else 
        {
            wrongWayTimer = 0f; // Reseta se voltar ao sentido certo
        }
    }

    void StopRecovery() 
    {
        isRecovering = false;
        recoveryGraceTimer = 3f; // Dá 3 segundos para a IA acelerar antes de julgar se está presa de novo
        SetNearestWaypointAsTarget();

        Vector3 targetDir = (waypoints[currentTargetIndex].position - transform.position).normalized;
        targetDir.y = 0; // Mantém o carro reto no horizonte
        transform.forward = targetDir;
    }

    float CalculateRecoverySteer()
    {
        Vector3 localTarget = transform.InverseTransformPoint(waypoints[currentTargetIndex].position);
        
        // Se o alvo está atrás de nós (z negativo), precisamos girar forte
        if (localTarget.z < 0)
        {
            // Gira para o lado oposto ao que o alvo está em relação à nossa traseira
            return localTarget.x > 0 ? 1f : -1f;
        }

        return localTarget.x > 0 ? 1f : -1f;
    }

    #region rubber banding
    public void SetHuntingGroup(bool focusOnLeader) => huntsLeader = focusOnLeader;

    private float ApplyRubberBanding(float baseThrottle)
    {
        // 1. Definição do Alvo
        RacerStatus myTarget = huntsLeader ? RaceManager.Instance.HumanLeader : RaceManager.Instance.HumanTrailer;
        if (myTarget == null) return baseThrottle;

        SCR_RayBasedCarPhysics targetPhysics = myTarget.GetComponent<SCR_RayBasedCarPhysics>();
        RacerStatus myStatus = GetComponent<RacerStatus>();
        
        float dist = Vector3.Distance(transform.position, myTarget.transform.position);
        bool isAheadOfTarget = myStatus.position < myTarget.position;
        bool isTargetHover = targetPhysics != null && targetPhysics.carType == CarType.hover;

        // Distâncias de controle (Sintonize aqui)
        float minCatchUpDist = 20f;
        float maxCatchUpDist = 150f; // A partir daqui, a IA usa o boost máximo
        float minWaitDist = 40f;
        float maxWaitDist = 180f;  // A partir daqui, a IA usa o debuff máximo

        // Valor padrão (IA em performance normal)
        float factor = 1.0f;

        // --- CATCH UP (IA ATRÁS) ---
        if (!isAheadOfTarget && dist > minCatchUpDist)
        {
            // Interpolação linear de 0 a 1 baseada na distância
            float t = Mathf.InverseLerp(minCatchUpDist, maxCatchUpDist, dist);
            
            // Curva suave para não dar trancos (S-Curve)
            t = t * t * (3f - 2f * t);

            // O fator vai de 1.0 até maxCatchUpBoost
            float targetBoost = maxCatchUpBoost;

            // Bônus estratégico contra Hovers (eles são mais rápidos nas retas)
            //if (isTargetHover) targetBoost *= 1.15f; 

            factor = Mathf.Lerp(1.0f, targetBoost, t);
            //currentSkillModifier = hardSkillModifier;
        }
        // --- WAIT (IA À FRENTE) ---
        else if (isAheadOfTarget && dist > minWaitDist)
        {
            float t = Mathf.InverseLerp(minWaitDist, maxWaitDist, dist);
            t = t * t * (3f - 2f * t);

            // O fator vai de 1.0 até maxWaitSlowdown (ex: 0.75)
            float targetWait = maxWaitSlowdown;

            // Se o player for hover, a IA espera menos (fica mais rápida) pois o player recupera rápido
            if (isTargetHover) targetWait = Mathf.Min(1.0f, targetWait * 1.2f);

            factor = Mathf.Lerp(1.0f, targetWait, t);
            //currentSkillModifier = easySkillModifier;
        }
        else
        {
            // Zona de conforto (IA perto do player)
            factor = 1.0f;
            //currentSkillModifier = 1.0f;
        }

        // --- APLICAÇÃO FINAL ---
        
        // 1. Atualiza a variável no script de física (afeta aceleração e freio)
        car.rubberBandingFactor = factor;

        return baseThrottle;
    }
    #endregion

    private void OnDrawGizmos()
    {
        if (sensorPivot == null) return;
        Gizmos.color = currentState == AIState.Overtaking ? Color.red : (currentState == AIState.Defending ? Color.blue : Color.green);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 5, 2f);
        
        // Desenha o LookAhead Point Real
        Gizmos.color = Color.yellow;
        //Gizmos.DrawSphere(GetLookAheadPoint(), 2f);
    }

    #region Turbo e drift

    private void AnalyzeTrackAhead(out float averageCurvature, out float straightDistance, out Vector3 targetDirection)
    {
        averageCurvature = 0f;
        straightDistance = 0f;
        int lookAheadSteps = 5; 
        
        // O targetDirection padrão agora também é calculado no espaço local
        targetDirection = transform.forward; 

        for (int i = 0; i < lookAheadSteps; i++)
        {
            int index = (currentTargetIndex + i) % waypoints.Count;
            int nextIndex = (index + 1) % waypoints.Count;
            
            Vector3 worldDirToWp = (waypoints[nextIndex].position - waypoints[index].position).normalized;

            Vector3 localDirToWp = transform.InverseTransformDirection(worldDirToWp);
            
            // Ignoramos a inclinação (Y local), focando apenas se a pista vira para os lados
            localDirToWp.y = 0;
            localDirToWp.Normalize();

            // Calculamos o ângulo em relação ao "Forward" local do carro
            // Se localDirToWp.z for 1, a pista está reta na frente do para-brisa
            float angle = Vector3.Angle(Vector3.forward, localDirToWp);
            if (angle > 5f) averageCurvature += angle;

            if (i == 1) targetDirection = worldDirToWp; 

            // Se o ângulo local for pequeno, contamos como reta
            if (angle < 10f) straightDistance += Vector3.Distance(waypoints[index].position, waypoints[nextIndex].position);
        }
        averageCurvature /= lookAheadSteps;
    }

    void HandleAdvancedDriving()
    {
        AnalyzeTrackAhead(out float curvature, out float straightLen, out Vector3 targetTrackDir);

        // --- THRESHOLDS MAIS ALTOS E DINÂMICOS ---
        // Aumentamos a base: IA Cautelosa só drifta em 50°, IA Agressiva em 30°
        float minDriftAngle = Mathf.Lerp(50f, 30f, profile.aggressiveness);
        
        // Velocidade mínima também baseada na agressividade (IAs agressivas tentam driftar mais devagar)
        float minSpeedForDrift = Mathf.Lerp(50f, 35f, profile.aggressiveness);

        Vector3 localDirToNextPoint = transform.InverseTransformPoint(waypoints[currentTargetIndex].position);
        localDirToNextPoint.y = 0;
        float angleToNextPointLocal = Vector3.Angle(Vector3.forward, localDirToNextPoint.normalized);

        // Pegamos a velocidade local (usando o seu script de física)
        // Se não tiver acesso ao currentCarLocalVelocity, use rb.transform.InverseTransformDirection(rb.linearVelocity)
        float forwardSpeed = rb.linearVelocity.z;

        if (!_isCurrentlyDrifting)
        {
            // CONDIÇÃO DE ENTRADA MUITO MAIS RIGOROSA
            if (curvature > minDriftAngle && 
                forwardSpeed > minSpeedForDrift && 
                angleToNextPointLocal > 15f) // Aumentado de 10 para 15 para evitar gatilhos bobos
            {
                _isCurrentlyDrifting = true;
            }
        }
        else 
        {
            // SAÍDA DE DRIFT
            // IAs agressivas seguram o drift por mais tempo
            float exitAngleThreshold = Mathf.Lerp(15f, 5f, profile.aggressiveness);
            
            Vector3 localTargetTrackDir = transform.InverseTransformDirection(targetTrackDir);
            localTargetTrackDir.y = 0;
            float angleToTargetDirLocal = Vector3.Angle(Vector3.forward, localTargetTrackDir.normalized);

            // Se o carro já alinhou ou ficou devagar demais, solta o freio de mão
            if (angleToTargetDirLocal < exitAngleThreshold || forwardSpeed < 25f || curvature < 10f)
            {
                _isCurrentlyDrifting = false;
            }
        }
    }

    #endregion
}

public enum AIDifficulty { Easy, Medium, Hard, UltraHard }