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

    [Header("Recuperação")]
    [SerializeField] private float stuckThreshold = 2f;

    [Header("Configurações de Erro")]
    [SerializeField] private float maxTracingError = 5.0f; // Metros máximos de desvio do traçado
    private float currentTracingNoise = 0f;
    private float noiseChangeTimer = 0f;
    private float currentSkillModifier = 1f; // Usado para o Rubber Banding depois
    [SerializeField] float hardSkillModifier = 2f;
    [SerializeField] float easySkillModifier = 0.3f;

    // Estado Interno
    private AIState currentState = AIState.Racing;
    private Rigidbody rb;
    private SCR_CarInput carInputs;
    private SCR_RayBasedCarPhysics car;
    private float currentSteerVelocity;
    private float stuckTimer = 0f;
    private bool isRecovering = false;
    private float lateralOffset = 0f; // Usado para ultrapassagem/defesa
    private float wrongWayTimer = 0f;
    private float lastSideDecisionTime;
    private float chosenSide = 0; // -1 (esquerda), 1 (direita)
    float recoveryGraceTimer;
    private float targetTracingNoise = 0f;

    private float smoothedSteerInput = 0f;

    SCR_RayBasedCarPhysics rayBasedPhysics;

    void OnEnable()//switch to onEnable later
    {
        car = GetComponent<SCR_RayBasedCarPhysics>();
        rb = GetComponent<Rigidbody>();
        carInputs = GetComponent<SCR_CarInput>();
        rayBasedPhysics = GetComponent<SCR_RayBasedCarPhysics>();
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        if (sensorPivot == null) sensorPivot = transform;

        if(rayBasedPhysics != null)
        {
            float accMult = 7f;
            float speedMult = 9f;
            float decelDivisor = 10.0f;

            rayBasedPhysics.SetAI(true, accMult, speedMult, decelDivisor, this);
            Debug.LogWarning($"Set AI parameters to {gameObject}");
        }

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
        DetermineAIState();

        float steeringInput = (currentState == AIState.Recovering) ? CalculateRecoverySteer() : CalculateSteering();
        float throttleInput = (currentState == AIState.Recovering) ? -0.5f : CalculateThrottle();

        if (carInputs != null)
        {
            // Interpolação do volante
            float currentInput = carInputs.GetCurrentInputState().steering;
            float dynamicSmoothness = Mathf.Lerp(steeringSmoothness, steeringSmoothness * 2f, Mathf.Abs(steeringInput));
            float finalSteer = Mathf.MoveTowards(currentInput, steeringInput, Time.fixedDeltaTime * dynamicSmoothness);
            
            carInputs.SetSteeringInput(finalSteer);
            carInputs.SetThrottleInput(throttleInput);

            // --- LÓGICA F-ZERO DRIFT ASSISTIDO ---
            // Se a curva for muito fechada (steering alto) e a velocidade for alta
            bool shouldDrift = Mathf.Abs(finalSteer) > 0.7f && rb.linearVelocity.magnitude > 40f;
            
            // Chamamos o método do seu script de física via SCR_CarInput ou direto se preferir
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
                rb.AddForce(-sideVelocity * rb.mass * gripCorrection, ForceMode.Force);
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

        // Opcional: Cancelar Invokes anteriores para garantir que o tempo de parada seja respeitado
        CancelInvoke("StopRecovery");
        Invoke("StopRecovery", Random.Range(0.25f, 1.5f)); // Tempo que ele ficará parado/congelado
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
        // --- 1. RUÍDO DE ERRO (Skill do Piloto) ---
        noiseChangeTimer -= Time.fixedDeltaTime;
        if (noiseChangeTimer <= 0)
        {
            float effectiveSkill = Mathf.Clamp01(profile.skillLevel * currentSkillModifier);
            float errorRange = (1f - effectiveSkill) * maxTracingError;
            targetTracingNoise = Random.Range(-errorRange, errorRange);
            noiseChangeTimer = Random.Range(3f, 6f);
        }
        currentTracingNoise = Mathf.Lerp(currentTracingNoise, targetTracingNoise, Time.fixedDeltaTime * 0.5f);

        // --- 2. LOOK-AHEAD DINÂMICO ---
        // Em baixa velocidade olha perto (curvas fechadas), em alta olha longe (estabilidade).
        float speedMS = rb.linearVelocity.magnitude;
        float dynamicLookDistance = lookAheadDistance + (speedMS * lookAheadSpeedFactor);
        Vector3 targetPos = GetLookAheadPointCustom(dynamicLookDistance); 

        // --- 3. OFFSET DE COMBATE E SEGURANÇA ---
        float avoidance = GetDifferentialAvoidance();
        float safetyFilter = Mathf.Clamp01(1.0f - (Mathf.Abs(avoidance) * 1.5f));
        
        float targetLateralOffset = lateralOffset;
        if (currentState == AIState.Defending) targetLateralOffset *= 1.2f;

        float effectiveOffset = (targetLateralOffset + currentTracingNoise) * safetyFilter;
        targetPos += transform.right * effectiveOffset;

        // --- 4. CÁLCULO DE ÂNGULO E DEADZONE DINÂMICA ---
        Vector3 localTarget = transform.InverseTransformPoint(targetPos);
        float angleToTarget = Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg;

        // Se estiver lento, a zona morta é grande (8°). Se rápido, é pequena (1°).
        float dynamicDeadzone = Mathf.Lerp(8.0f, 6.0f, speedMS / 20f);
        
        float rawSteerInput = 0f;
        if (Mathf.Abs(angleToTarget) > dynamicDeadzone)
        {
            rawSteerInput = angleToTarget / maxSteerAngle;
            // Curva de potência para suavizar o centro (mais controle)
            rawSteerInput = Mathf.Sign(rawSteerInput) * Mathf.Pow(Mathf.Abs(rawSteerInput), 1.2f);
        }

        // --- 5. FILTRO LOW-PASS (O fim do tremor) ---
        // Impede que o valor do input mude instantaneamente de um frame para o outro.
        float steerSmoothSpeed = Mathf.Lerp(2f, 5f, speedMS / 60f); 
        smoothedSteerInput = Mathf.MoveTowards(smoothedSteerInput, rawSteerInput, Time.fixedDeltaTime * steerSmoothSpeed);

        return Mathf.Clamp(smoothedSteerInput + avoidance, -1f, 1f);
    }

    // Método auxiliar para suportar a distância dinâmica
    Vector3 GetLookAheadPointCustom(float distance)
    {
        Vector3 currentWp = waypoints[currentTargetIndex].position;
        int nextIndex = (currentTargetIndex + 1) % waypoints.Count;
        Vector3 nextWp = waypoints[nextIndex].position;

        Vector3 segmentDir = (nextWp - currentWp).normalized;
        float segmentLength = Vector3.Distance(currentWp, nextWp);
        float dot = Vector3.Dot(transform.position - currentWp, segmentDir);
        float targetProgress = Mathf.Clamp(dot, 0, segmentLength) + distance;

        if (targetProgress > segmentLength)
        {
            int afterNextIndex = (nextIndex + 1) % waypoints.Count;
            Vector3 secondDir = (waypoints[afterNextIndex].position - nextWp).normalized;
            return nextWp + secondDir * (targetProgress - segmentLength);
        }
        return currentWp + (segmentDir * targetProgress);
    }

    Vector3 GetLookAheadPoint()
    {
        // O lookAhead base (ex: 20) somado à velocidade (m/s) multiplicada pelo fator.
        // Se o carro está a 50m/s (180km/h), ele olhará 20 + (50 * 1.5) = 95 metros à frente.
        float dynamicLookAhead = lookAheadDistance + (rb.linearVelocity.magnitude * lookAheadSpeedFactor);
        
        Vector3 currentWp = waypoints[currentTargetIndex].position;
        int nextIndex = (currentTargetIndex + 1) % waypoints.Count;
        Vector3 nextWp = waypoints[nextIndex].position;

        Vector3 segmentDir = (nextWp - currentWp).normalized;
        float segmentLength = Vector3.Distance(currentWp, nextWp);
        
        // Calcula o progresso atual do carro no segmento entre os waypoints
        float dot = Vector3.Dot(transform.position - currentWp, segmentDir);
        float targetProgress = Mathf.Clamp(dot, 0, segmentLength) + dynamicLookAhead;

        // Se o ponto dinâmico ultrapassar o waypoint atual, ele projeta para o próximo segmento
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

        float currentSpeedMS = rb.linearVelocity.magnitude;
        float targetSpeedMS = (profile.baseTargetSpeedKmh / 3.6f);

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
                if(!IsInvoking("StopRecovery")) Invoke("StopRecovery", 2.5f);
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
        RacerStatus myTarget = huntsLeader ? RaceManager.Instance.HumanLeader : RaceManager.Instance.HumanTrailer;
        if (myTarget == null) return baseThrottle;

        // --- LÓGICA DE DETECÇÃO DE HOVER NO ALVO ---
        float hoverSpeedBoost = 1.0f;
        car.rubberBandingAccelerationMultiplier = 1f;

        SCR_RayBasedCarPhysics targetPhysics = myTarget.GetComponent<SCR_RayBasedCarPhysics>();
        
        // Se o alvo for um Hover, aumentamos a agressividade da perseguição em 20%
        bool isTargetHover = targetPhysics != null && targetPhysics.carType == CarType.hover;
        if (isTargetHover)
        {
            hoverSpeedBoost = 1.5f; 
        }

        RacerStatus myStatus = GetComponent<RacerStatus>();
        float dist = Vector3.Distance(transform.position, myTarget.transform.position);
        bool isAheadOfTarget = myStatus.position < myTarget.position;

        float speedMod = 1.0f;
        currentSkillModifier = 1.0f; 

        // --- CATCH UP (IA ATRÁS) ---
        if (!isAheadOfTarget && dist > 20f) 
        {
            float intensity = Mathf.Pow(dist / 50f, 2f); 
            
            // Aplicamos o hoverSpeedBoost aqui no multiplicador de velocidade
            speedMod = Mathf.Clamp(1.0f + intensity, 1.0f, maxCatchUpBoost * hoverSpeedBoost);

            float accelBoost = isTargetHover ? hoverTargetAccelerationBoost : classicTargetAccelerationBoost; // Contra Hover a IA precisa de mais torque
            car.rubberBandingAccelerationMultiplier = Mathf.Lerp(1f, accelBoost, intensity);

            currentSkillModifier = hardSkillModifier; 
        }
        // --- WAIT (IA MUITO À FRENTE) ---
        else if (isAheadOfTarget && dist > 40f)
        {
            float slowIntensity = Mathf.Clamp01((dist - 40f) / 100f);
            
            // Se o player estiver em hover, a IA "espera menos" (fica mais rápida mesmo no modo wait)
            float waitLimit = targetPhysics.carType == CarType.hover ? maxWaitSlowdown * 1.1f : maxWaitSlowdown;
            speedMod = Mathf.Lerp(1.0f, waitLimit, slowIntensity);

            car.rubberBandingAccelerationMultiplier = Mathf.Lerp(1.0f, waitAccelerationDecrease, slowIntensity);

            currentSkillModifier = Mathf.Lerp(1.0f, easySkillModifier, slowIntensity);
        }

        float baseTargetSpeed = (profile.baseTargetSpeedKmh / 3.6f);
        rayBasedPhysics.SetMaxSpeed(baseTargetSpeed * speedMod);

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
}