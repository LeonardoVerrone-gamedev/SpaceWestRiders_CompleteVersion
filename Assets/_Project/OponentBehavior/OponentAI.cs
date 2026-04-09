using UnityEngine;
using System.Collections.Generic;

public enum AIState { Racing, Overtaking, Defending, Recovering }

public class AIRacingController : MonoBehaviour
{
    [Header("Perfil do Piloto")]
    [SerializeField] private SO_AIOponentProfile profile;

    [Header("Targeting")]
    [SerializeField] private List<Transform> waypoints = new List<Transform>();
    [SerializeField] private int currentTargetIndex = 0;
    [SerializeField] private float waypointPassRadius = 30f;
    [SerializeField] private float lookAheadDistance = 20f;
    [SerializeField] private float lookAheadSpeedFactor = 1.5f;

    [Header("Rubber banding")]
    private bool huntsLeader = true;
    public bool forceHuntLeader;

    [Header("Rubber Banding Settings")]
    [SerializeField] float maxCatchUpBoost = 1.5f;
    [SerializeField] float maxWaitSlowdown = 0.75f;

    [Header("Sensores e Detecção")]
    [SerializeField] private Transform sensorPivot;
    [SerializeField] private LayerMask wallLayer;
    [SerializeField] private LayerMask opponentLayer;
    [SerializeField] private float opponentDetectDist = 80f;
    [SerializeField] private float sideSensorDist = 8f;      
    [SerializeField] private float diagonalSensorDist = 45f; 
    [SerializeField] private float frontSensorDist = 120f;    
    [SerializeField] private float avoidanceForce = 5.0f;
    [SerializeField] private float sideDecisionCooldown = 1.5f;

    [Header("Car Movement")]
    [SerializeField] private float maxSteerAngle = 15f; 
    [SerializeField] private float steeringSmoothness = 15f;

    [Header("Configurações de Erro")]
    [SerializeField] private float maxTracingError = 5.0f;
    private float currentTracingNoise = 0f;
    private float noiseChangeTimer = 0f;

    [Header("Turbo e drift")]
    [SerializeField] private float driftThresholdAngle = 25f;
    [SerializeField] private float turboStraightReach = 150f;
    [SerializeField] private float minStaminaForTurbo = 20f;

    private AIState currentState = AIState.Racing;
    private Rigidbody rb;
    private SCR_CarInput carInputs;
    private SCR_RayBasedCarPhysics car;
    private float stuckTimer = 0f;
    private bool isRecovering = false;
    private float lateralOffset = 0f;
    private float wrongWayTimer = 0f;
    private float lastSideDecisionTime;
    private float chosenSide = 0;
    float recoveryGraceTimer;
    private float targetTracingNoise = 0f;

    private bool _isCurrentlyDrifting = false;
    float _lastSteerOutput;

    private float myUniqueLaneOffset;

    SCR_RayBasedCarPhysics rayBasedPhysics;

    float berserkTimer = 0f;

    [SerializeField] public RubberBandingValues rubberBandingValues;

    float nextAggressionDecisionTime;
    bool decidedToBrakeCheck;

    float nextDefenseDecisionTime;
    bool decidedToDefend;

    float nextOvertakeDecisionTime;
    bool decidedToOvertake;

    float nextTurboDecision;

    [Header("Steering Improvements")]
    [SerializeField] private AnimationCurve speedBasedSteeringCurve = AnimationCurve.EaseInOut(0, 25, 200, 12);
    [SerializeField] private float steeringPrediction = 1.2f;
    private float lastSteeringError;

    [Header("Corner Planning")]
    [SerializeField] private int cornerLookaheadPoints = 3;
    [SerializeField] private float cornerBrakingDistance = 50f;
    private float upcomingCornerAngle;
    private float distanceToCorner;

    [Header("State Smoothing")]
    [SerializeField] private float stateTransitionTime = 0.5f;

    [Header("Drift Control")]
    [SerializeField] private float driftSteeringSensitivity = 0.6f;
    [SerializeField] private float driftAngleCorrectionSpeed = 3f;

    void OnEnable()
    {
        car = GetComponent<SCR_RayBasedCarPhysics>();
        rb = GetComponent<Rigidbody>();
        carInputs = GetComponent<SCR_CarInput>();
        rayBasedPhysics = GetComponent<SCR_RayBasedCarPhysics>();
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        if (sensorPivot == null) sensorPivot = transform;

        SetAI();

        waypoints.Clear();
        SCR_WaypointHolder holder = FindFirstObjectByType<SCR_WaypointHolder>();

        if (holder != null)
        {
            waypoints = holder.waypoints;
        }
    }

    void Start()
    {
        myUniqueLaneOffset = Random.Range(-10f, 10f);
    }

    public void SetAI()
    {
        if (rayBasedPhysics != null)
        {
            rayBasedPhysics.SetAI(true, this);
        }
    }

    void FixedUpdate()
    {
        if (waypoints == null || waypoints.Count == 0) return;

        if (isRecovering) 
        {
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

        UpdateAIDecisions();

        DetermineAIState();

        float steeringInput = (currentState == AIState.Recovering) ? CalculateRecoverySteer() : CalculateSteering();
        float throttleInput = (currentState == AIState.Recovering) ? -0.5f : CalculateThrottle();

        if (carInputs != null)
        {
            carInputs.SetSteeringInput(steeringInput);
            carInputs.SetThrottleInput(throttleInput);

            if (currentState == AIState.Racing && car.IsGrounded)
            {
                Vector3 vel = rb.linearVelocity;
                Vector3 side = transform.right;
                side.y = 0;
                side.Normalize();

                float sideVelMag = Vector3.Dot(vel, side);
                Vector3 sideVelocity = side * sideVelMag;
                
                float gripCorrection = Mathf.Lerp(2.0f, 0.5f, Mathf.Abs(carInputs.GetCurrentInputState().steering));
            }
        }
    }

    void UpdateAIDecisions()
    {
        if (Time.time > nextDefenseDecisionTime)
        {
            decidedToDefend = Random.value < Mathf.Clamp01((profile.skillLevel * 0.3f) + (profile.defensiveSkill * 0.7f));
            nextDefenseDecisionTime = Time.time + Random.Range(0.5f, 1.5f);
        }

        if (Time.time > nextAggressionDecisionTime)
        {
            decidedToBrakeCheck = Random.value < profile.aggressiveness * 0.475f;
            nextAggressionDecisionTime = Time.time + Random.Range(1.0f, 2.5f);
        }

        if (Time.time > nextOvertakeDecisionTime)
        {
            decidedToOvertake = Random.value < profile.aggressiveness;
            nextOvertakeDecisionTime = Time.time + Random.Range(0.4f, 1.2f);
        }
    }

    public void SetRecovering()
    {
        isRecovering = true;
        currentState = AIState.Recovering;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        float recoveryTime = Random.Range(0.2f, 1f);

        CancelInvoke("StopRecovery");
        Invoke("StopRecovery", recoveryTime);
    }

    void DetermineAIState()
    {
        if (isRecovering) { currentState = AIState.Recovering; return; }

        RaycastHit hit;
        Vector3 fwd = sensorPivot.forward;
        Vector3 backDir = -sensorPivot.forward;
        float transitionSpeed = Mathf.Lerp(5f, 2f, rb.linearVelocity.magnitude / 150f);

        float defenseRange = opponentDetectDist * 0.6f;
        if (Physics.BoxCast(sensorPivot.position, new Vector3(2.5f, 2f, 1f), backDir, out hit, sensorPivot.rotation, defenseRange, opponentLayer))
        {
            if (hit.transform != transform && decidedToDefend)
            {
                currentState = AIState.Defending;
                Vector3 opponentLocalPos = transform.InverseTransformPoint(hit.transform.position);
                
                if (Mathf.Abs(opponentLocalPos.x) > 2.0f)
                    chosenSide = opponentLocalPos.x > 0 ? 1f : -1f;

                ApplyLateralMovement(transitionSpeed);
                return;
            }

            if (decidedToBrakeCheck)
            {
                carInputs.SetThrottleInput(-0.4f);
            }

        }

        if (Physics.BoxCast(sensorPivot.position, new Vector3(2f, 2f, 2f), fwd, out hit, sensorPivot.rotation, opponentDetectDist, opponentLayer))
        {
            float myForwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
            
            Rigidbody opponentRb = hit.rigidbody;
            float opponentForwardSpeed = (opponentRb != null) ? Vector3.Dot(opponentRb.linearVelocity, transform.forward) : 0;

            float relativeSpeedZ = myForwardSpeed - opponentForwardSpeed;

            if (hit.transform != transform && decidedToOvertake && profile.aggressiveness > 0.3f && (relativeSpeedZ > 2f || hit.distance < 15f))
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
                    currentState = AIState.Racing;
                }
                return;
            }
        }

        currentState = AIState.Racing;
        chosenSide = 0;

        float flowPush = CalculateFlowAvoidance();
        float targetFlowOffset = flowPush * 5f; 

        lateralOffset = Mathf.MoveTowards(lateralOffset, targetFlowOffset, Time.fixedDeltaTime * transitionSpeed);
    }

    void ApplyLateralMovement(float transitionSpeed)
    {
        float targetAmt = (currentState == AIState.Overtaking ? 6f : 4f) * chosenSide;
        Vector3 sideDir = transform.right * chosenSide;

        RaycastHit wallHit;
        bool wallAhead = Physics.Raycast(sensorPivot.position, (transform.forward + sideDir).normalized, out wallHit, sideSensorDist * 2f, wallLayer);
        bool wallSide = Physics.Raycast(sensorPivot.position, sideDir, out wallHit, sideSensorDist, wallLayer);

        if (wallAhead || wallSide)
        {
            lateralOffset = Mathf.MoveTowards(lateralOffset, 0, Time.fixedDeltaTime * 15f);
        }
        else
        {
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
            targetTracingNoise = Random.Range(-errorRange, errorRange);
            noiseChangeTimer = Random.Range(2f, 4f);
        }
        currentTracingNoise = Mathf.Lerp(currentTracingNoise, targetTracingNoise, Time.fixedDeltaTime * 3f);

        Vector3 targetPos = GetImprovedLookAheadPoint();
        
        float cornerSlowdown = PlanForUpcomingCorner();
        
        int nextIndex = (currentTargetIndex + 1) % waypoints.Count;
        Vector3 trackDir = (waypoints[nextIndex].position - waypoints[currentTargetIndex].position).normalized;
        Vector3 trackRight = Vector3.Cross(Vector3.up, trackDir).normalized;
        
        float avoidance = GetDifferentialAvoidance();
        float wallDanger = Mathf.Abs(avoidance);
        float safetyFilter = Mathf.Clamp01(1.0f - (wallDanger * 1.2f));
        
        float targetLateralOffset = lateralOffset;
        if (currentState == AIState.Defending) targetLateralOffset *= 1.2f;
        
        float effectiveOffset = (targetLateralOffset + currentTracingNoise + myUniqueLaneOffset) * safetyFilter;
        targetPos += trackRight * effectiveOffset;
        
        Vector3 predictedPosition = transform.position + rb.linearVelocity * steeringPrediction * Time.fixedDeltaTime;
        Vector3 predictedDirection = targetPos - predictedPosition;
        
        float angleToTarget;
        
        if (_isCurrentlyDrifting)
        {
            Vector3 velocityDir = rb.linearVelocity.normalized;
            Vector3 localVelocityDir = transform.InverseTransformDirection(velocityDir);
            Vector3 localTargetDir = transform.InverseTransformDirection(predictedDirection.normalized);
            
            float velocityAngle = Mathf.Atan2(localVelocityDir.x, localVelocityDir.z) * Mathf.Rad2Deg;
            float targetAngle = Mathf.Atan2(localTargetDir.x, localTargetDir.z) * Mathf.Rad2Deg;
            
            float driftAngle = Vector3.Angle(velocityDir, transform.forward);
            float desiredCorrection = targetAngle - velocityAngle;
            
            angleToTarget = Mathf.Clamp(desiredCorrection, -driftSteeringSensitivity * 50f, driftSteeringSensitivity * 50f);
            
            float driftFactor = Mathf.Clamp01(driftAngle / 45f);
            angleToTarget *= (1f - driftFactor * 0.5f);
        }
        else
        {
            Vector3 localDir = transform.InverseTransformDirection(predictedDirection.normalized);
            localDir.y = 0;
            angleToTarget = Vector3.SignedAngle(Vector3.forward, localDir.normalized, Vector3.up);
        }
        
        float currentSpeed = rb.linearVelocity.magnitude;
        float dynamicMaxSteer = speedBasedSteeringCurve.Evaluate(currentSpeed);
        float steeringSensitivity = Mathf.Lerp(0.8f, 1.2f, profile.aggressiveness);
        
        float rawSteerInput = 0f;
        float dynamicDeadzone = Mathf.Lerp(12f, 5f, currentSpeed / 100f);
        
        if (Mathf.Abs(angleToTarget) > dynamicDeadzone)
        {
            rawSteerInput = Mathf.Clamp(angleToTarget / dynamicMaxSteer, -1f, 1f);
            rawSteerInput = Mathf.Sign(rawSteerInput) * Mathf.Pow(Mathf.Abs(rawSteerInput), 1.3f);
            rawSteerInput *= steeringSensitivity;
        }
        
        if (_isCurrentlyDrifting)
        {
            rawSteerInput *= 0.8f;
        }
        
        float currentError = Mathf.Abs(angleToTarget);
        if (currentError < 5f && Mathf.Abs(lastSteeringError) < 5f && !_isCurrentlyDrifting)
        {
            rawSteerInput *= 0.7f;
        }
        lastSteeringError = currentError;
        
        float finalTarget = Mathf.Clamp(rawSteerInput + (avoidance * 0.3f), -1f, 1f);
        
        float steerLerpSpeed = (Mathf.Abs(finalTarget) < 0.1f && !_isCurrentlyDrifting) ? 20f : 12f;
        steerLerpSpeed = Mathf.Lerp(steerLerpSpeed, steerLerpSpeed * 1.5f, profile.skillLevel);
        
        _lastSteerOutput = Mathf.MoveTowards(_lastSteerOutput, finalTarget, Time.fixedDeltaTime * steerLerpSpeed);
        
        return _lastSteerOutput;
    }

    Vector3 GetImprovedLookAheadPoint()
    {
        float forwardSpeed = Mathf.Max(5f, rb.linearVelocity.magnitude);
        float dynamicLookAhead = lookAheadDistance + (forwardSpeed * lookAheadSpeedFactor * 0.05f);
        
        dynamicLookAhead = Mathf.Min(dynamicLookAhead, 80f);
        
        float accumulatedDistance = 0f;
        int currentIdx = currentTargetIndex;
        Vector3 lastPoint = waypoints[currentIdx].position;
        
        while (accumulatedDistance < dynamicLookAhead)
        {
            int nextIdx = (currentIdx + 1) % waypoints.Count;
            float segmentLength = Vector3.Distance(waypoints[currentIdx].position, waypoints[nextIdx].position);
            
            if (accumulatedDistance + segmentLength >= dynamicLookAhead)
            {
                float t = (dynamicLookAhead - accumulatedDistance) / segmentLength;
                return Vector3.Lerp(waypoints[currentIdx].position, waypoints[nextIdx].position, t);
            }
            
            accumulatedDistance += segmentLength;
            lastPoint = waypoints[nextIdx].position;
            currentIdx = nextIdx;
            
            if (currentIdx == currentTargetIndex) break;
        }
        
        return lastPoint;
    }

    float PlanForUpcomingCorner()
    {
        float totalAngle = 0f;
        float totalDistance = 0f;
        int pointsChecked = 0;
        
        int checkIdx = currentTargetIndex;
        
        for (int i = 0; i < cornerLookaheadPoints; i++)
        {
            int nextIdx = (checkIdx + 1) % waypoints.Count;
            Vector3 toNext = (waypoints[nextIdx].position - waypoints[checkIdx].position).normalized;
            
            if (i > 0)
            {
                Vector3 prevDir = (waypoints[checkIdx].position - waypoints[(checkIdx - 1 + waypoints.Count) % waypoints.Count].position).normalized;
                float angle = Vector3.Angle(prevDir, toNext);
                totalAngle += angle;
            }
            
            totalDistance += Vector3.Distance(waypoints[checkIdx].position, waypoints[nextIdx].position);
            checkIdx = nextIdx;
            pointsChecked++;
            
            if (checkIdx == currentTargetIndex) break;
        }
        
        float averageAngle = totalAngle / Mathf.Max(1, pointsChecked - 1);
        upcomingCornerAngle = averageAngle;
        distanceToCorner = totalDistance;

        float angleThreshold = (car.carType == CarType.hover) ? 50f : 30f;
        
        if (averageAngle > angleThreshold && totalDistance < cornerBrakingDistance)
        {
            float severity = Mathf.Clamp01((averageAngle - 30f) / 60f);
            
            float reductionStrength = 0.7f;

            if (car.carType == CarType.hover) 
            {
                reductionStrength = 0.3f; 
            }

            float speedFactor = 1f - (severity * reductionStrength);
            
            speedFactor = Mathf.Lerp(speedFactor, 1f, profile.skillLevel * 0.5f);
            
            return speedFactor;
        }
        
        return 1f;
    }

    Vector3 GetLookAheadPoint()
    {
        float forwardSpeed = rb.linearVelocity.z;
        float dynamicLookAhead = lookAheadDistance + (Mathf.Max(0, forwardSpeed) * lookAheadSpeedFactor);
        
        Vector3 currentWp = waypoints[currentTargetIndex].position;
        int nextIndex = (currentTargetIndex + 1) % waypoints.Count;
        Vector3 nextWp = waypoints[nextIndex].position;

        Vector3 segmentDir = (nextWp - currentWp).normalized;
        float segmentLength = Vector3.Distance(currentWp, nextWp);
        
        float dot = Vector3.Dot(transform.position - currentWp, segmentDir);
        
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

        currentTargetIndex = (closestIndex + 1) % waypoints.Count;
    }

    float CalculateThrottle()
    {
        if (isRecovering)
        {
            float dot = Vector3.Dot(transform.forward, (waypoints[currentTargetIndex].position - transform.position).normalized);
            return dot < 0 ? -0.5f : 0.8f;
        }
        
        float cornerSlowdown = PlanForUpcomingCorner();
        
        float finalThrottle = 1.0f * cornerSlowdown;
        
        Vector3 localTarget = transform.InverseTransformPoint(waypoints[currentTargetIndex].position);
        float angle = Mathf.Abs(Mathf.Atan2(localTarget.x, localTarget.z) * Mathf.Rad2Deg);
        
        float effectiveAngle = Mathf.Max(angle, upcomingCornerAngle * 0.5f);
        
        if (effectiveAngle > 15f)
        {
            float brakingCurve = Mathf.Lerp(0.9f, 0.3f, profile.brakingAbility);
            float brakeIntensity = Mathf.Clamp01((effectiveAngle - 15f) / 75f);
            finalThrottle = Mathf.Lerp(1.0f, -0.3f, brakeIntensity * brakingCurve);
            
            if (effectiveAngle > 30f && effectiveAngle < 60f && finalThrottle < 0.5f)
            {
                finalThrottle = Mathf.Lerp(finalThrottle, 0.2f, 0.5f);
            }
        }
        
        if (_isCurrentlyDrifting)
        {
            finalThrottle = Mathf.Lerp(0.5f, 0.8f, profile.aggressiveness);
        }
        
        if (currentState == AIState.Overtaking) 
        {
            finalThrottle = Mathf.Max(finalThrottle, 0.85f);
        }
        
        if (currentState == AIState.Defending && distanceToCorner < 40f)
        {
            finalThrottle = Mathf.Min(finalThrottle, 0.7f);
        }
        
        ApplyRubberBanding();
        finalThrottle *= car.rubberBandingFactor;
        
        return Mathf.Clamp(finalThrottle, -0.5f, 1.0f);
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
    
        if (recoveryGraceTimer > 0)
        {
            recoveryGraceTimer -= Time.fixedDeltaTime;
            return;
        }

        if (rb.linearVelocity.magnitude < 2f)
        {
            float stuckThreshold = Random.Range(0.15f, 1f);
            stuckTimer += Time.fixedDeltaTime;
            if (stuckTimer > stuckThreshold) 
            { 
                SetRecovering();
            }
        }
        else stuckTimer = 0f;
    }

    void CheckDirection()
    {
        if (isRecovering) return;

        if (Mathf.Abs(transform.up.y) < 0.5f) return;

        Vector3 wpPos = waypoints[currentTargetIndex].position;
        int prevIndex = currentTargetIndex == 0 ? waypoints.Count - 1 : currentTargetIndex - 1;
        Vector3 trackDirection = (wpPos - waypoints[prevIndex].position).normalized;
        
        float dot = Vector3.Dot(transform.forward, trackDirection);

        if (dot < -0.2f && rb.linearVelocity.magnitude > 2f)
        {
            wrongWayTimer += Time.fixedDeltaTime;
            if (wrongWayTimer > 1.2f) 
            {
                isRecovering = true;
                wrongWayTimer = 0f;
                if(!IsInvoking("StopRecovery")) Invoke("StopRecovery", Random.Range(0.25f, 1.5f));
            }
        }
        else 
        {
            wrongWayTimer = 0f;
        }
    }

    void StopRecovery() 
    {
        isRecovering = false;
        recoveryGraceTimer = 3f;
        SetNearestWaypointAsTarget();

        Vector3 targetDir = (waypoints[currentTargetIndex].position - transform.position).normalized;
        targetDir.y = 0;
        transform.forward = targetDir;
    }

    float CalculateRecoverySteer()
    {
        Vector3 localTarget = transform.InverseTransformPoint(waypoints[currentTargetIndex].position);
        
        if (localTarget.z < 0)
        {
            return localTarget.x > 0 ? 1f : -1f;
        }

        return localTarget.x > 0 ? 1f : -1f;
    }

    private float CalculateFlowAvoidance()
    {
        float flowRepulsion = 0f;
        float personalBubbleRadius = 12f;

        Collider[] nearbyOpponents = Physics.OverlapSphere(transform.position, personalBubbleRadius, opponentLayer);

        foreach (var col in nearbyOpponents)
        {
            if (col.transform == transform) continue;

            Vector3 localOpponentPos = transform.InverseTransformPoint(col.transform.position);

            if (Mathf.Abs(localOpponentPos.z) < 10f) 
            {
                float proximityFactor = 1f - (Mathf.Abs(localOpponentPos.x) / personalBubbleRadius);
                
                float side = Mathf.Sign(localOpponentPos.x);
                flowRepulsion -= side * proximityFactor;
            }
        }

        return flowRepulsion;
    }

    #region rubber banding
    public void SetHuntingGroup(bool focusOnLeader) => huntsLeader = focusOnLeader;

    private void ApplyRubberBanding()
    {
        if (!rubberBandingValues.useRubberBanding)
        {
            car.rubberBandingFactor = 1f;
            return;
        }

        RacerStatus myStatus = GetComponent<RacerStatus>();
        if (myStatus == null || myStatus.waypoints == null || myStatus.waypoints.Count == 0)
        {
            car.rubberBandingFactor = 1f;
            return;
        }

        RacerStatus target = huntsLeader
            ? RaceManager.Instance.HumanLeader
            : RaceManager.Instance.HumanTrailer;

        if (target == null)
        {
            car.rubberBandingFactor = 1f;
            return;
        }

        float progressDiff = target.TrackProgress - myStatus.TrackProgress;
        bool isBehind = progressDiff > 0f;
        float absDiff = Mathf.Abs(progressDiff);

        int waypointCount = myStatus.waypoints.Count;

        float minCatchUpDist = waypointCount * 0.02f;
        float maxCatchUpDist = waypointCount * 0.15f;
        float minWaitDist    = waypointCount * 0.02f;
        float maxWaitDist    = waypointCount * 0.10f;

        float factor = 1f;

        if (isBehind && absDiff > minCatchUpDist)
        {
            float t = Mathf.InverseLerp(minCatchUpDist, maxCatchUpDist, absDiff);
            t = t * t * (3f - 2f * t);
            factor = Mathf.Lerp(1f, maxCatchUpBoost, t);
        }
        else if (!isBehind && absDiff > minWaitDist)
        {
            float t = Mathf.InverseLerp(minWaitDist, maxWaitDist, absDiff);
            t = t * t * (3f - 2f * t);
            factor = Mathf.Lerp(1f, maxWaitSlowdown, t);
        }

        car.rubberBandingFactor = factor;
    }
    #endregion

    private void OnDrawGizmos()
    {
        if (sensorPivot == null) return;
        Gizmos.color = currentState == AIState.Overtaking ? Color.red : (currentState == AIState.Defending ? Color.blue : Color.green);
        Gizmos.DrawWireSphere(transform.position + Vector3.up * 5, 2f);
        
        Gizmos.color = Color.yellow;
    }

    #region Turbo e drift

    private void AnalyzeTrackAhead(out float averageCurvature, out float straightDistance, out Vector3 targetDirection)
    {
        averageCurvature = 0f;
        straightDistance = 0f;
        int lookAheadSteps = 5; 
        
        targetDirection = transform.forward; 

        for (int i = 0; i < lookAheadSteps; i++)
        {
            int index = (currentTargetIndex + i) % waypoints.Count;
            int nextIndex = (index + 1) % waypoints.Count;
            
            Vector3 worldDirToWp = (waypoints[nextIndex].position - waypoints[index].position).normalized;

            Vector3 localDirToWp = transform.InverseTransformDirection(worldDirToWp);
            
            localDirToWp.y = 0;
            localDirToWp.Normalize();

            float angle = Vector3.Angle(Vector3.forward, localDirToWp);
            if (angle > 5f) averageCurvature += angle;

            if (i == 1) targetDirection = worldDirToWp; 

            if (angle < 10f) straightDistance += Vector3.Distance(waypoints[index].position, waypoints[nextIndex].position);
        }
        averageCurvature /= lookAheadSteps;
    }

    void HandleAdvancedDriving()
    {
        AnalyzeTrackAhead(out float curvature, out float straightLen, out Vector3 targetTrackDir);
        
        float minDriftAngle = Mathf.Lerp(45f, 25f, profile.aggressiveness);
        float minSpeedForDrift = Mathf.Lerp(60f, 40f, profile.aggressiveness);
        float driftDuration = 0f;
        
        Vector3 localDirToNextPoint = transform.InverseTransformPoint(waypoints[currentTargetIndex].position);
        localDirToNextPoint.y = 0;
        float angleToNextPointLocal = Vector3.Angle(Vector3.forward, localDirToNextPoint.normalized);
        float forwardSpeed = rb.linearVelocity.magnitude;
        
        bool shouldDrift = false;
        
        if (!_isCurrentlyDrifting)
        {
            shouldDrift = (curvature > minDriftAngle || upcomingCornerAngle > minDriftAngle) && 
                        forwardSpeed > minSpeedForDrift && 
                        angleToNextPointLocal > 12f;
            
            if (shouldDrift)
            {
                float driftChance = Mathf.Lerp(0.3f, 0.8f, profile.aggressiveness);
                shouldDrift = Random.value < driftChance;
            }
            
            if (shouldDrift)
            {
                _isCurrentlyDrifting = true;
                driftDuration = 0f;
            }
        }
        else 
        {
            driftDuration += Time.fixedDeltaTime;
            
            float exitAngleThreshold = Mathf.Lerp(20f, 8f, profile.skillLevel);
            float maxDriftTime = Mathf.Lerp(1.5f, 3f, profile.aggressiveness);
            
            Vector3 velocityDir = rb.linearVelocity.normalized;
            Vector3 localVelocityDir = transform.InverseTransformDirection(velocityDir);
            localVelocityDir.y = 0;
            float currentDriftAngle = Vector3.Angle(Vector3.forward, localVelocityDir);
            
            Vector3 localTargetTrackDir = transform.InverseTransformDirection(targetTrackDir);
            localTargetTrackDir.y = 0;
            float angleToTargetDirLocal = Vector3.Angle(Vector3.forward, localTargetTrackDir.normalized);
            
            bool isAlignedWithTrack = Mathf.Abs(currentDriftAngle - angleToTargetDirLocal) < exitAngleThreshold;
            
            if (isAlignedWithTrack || 
                forwardSpeed < 30f || 
                curvature < 10f ||
                driftDuration > maxDriftTime)
            {
                _isCurrentlyDrifting = false;
            }
        }
        
        bool shouldTurbo = false;
        
        if (straightLen > turboStraightReach && !_isCurrentlyDrifting && car.GetNOSAmount() > minStaminaForTurbo)
        {
            float turboChance = 0f;
            
            switch(currentState)
            {
                case AIState.Overtaking:
                    turboChance = 0.85f;
                    break;
                case AIState.Defending:
                    turboChance = 0.5f;
                    break;
                case AIState.Racing:
                    turboChance = Mathf.Lerp(0.2f, 0.6f, profile.aggressiveness);
                    if (straightLen > turboStraightReach * 1.5f) turboChance += 0.2f;
                    break;
            }
            
            if (upcomingCornerAngle > 50f && distanceToCorner < 80f)
            {
                turboChance *= 0.3f;
            }
            
            if (Random.value < turboChance && Time.time > nextTurboDecision)
            {
                shouldTurbo = true;
                nextTurboDecision = Time.time + Random.Range(20f, 35f);
            }
        }
        
        if (shouldTurbo)
        {
            carInputs.TriggerTurbo(true);
            carInputs.TriggerTurbo(false);
        }
    }

    #endregion
}

[System.Serializable]
public class PursuitEvent
{
    public SCR_RayBasedCarPhysics targetCar;
    public float timeToInvoke;
}

[System.Serializable]
public class RubberBandingValues
{
    public float minCatchUpDist = 20f;
    public float maxCatchUpDist = 150f;
    public float minWaitDist = 40f;
    public float maxWaitDist = 180f;

    public bool useRubberBanding = false;

    public void SetRubberBandingValues(bool use, float minCatch, float maxCatch, float minWait, float maxWait)
    {
        useRubberBanding = use;
        minCatchUpDist = minCatch;
        maxCatchUpDist = maxCatch;
        minWaitDist = minWait;
        maxWaitDist = maxWait;
    }
}