using Unity.MLAgents;
using UnityEngine;
using UnityEngine.Splines;
using Unity.Mathematics;


public class RewardSystem
{
    private CarAgent agent;
    private TrainingManager trainingManager;
    private RewardDebugger debugger;
    

    private float lastDistanceToCheckpoint;
    private float timeStationary;
    private const float stationaryTimeout = 3f;
    private const float minSpeedThreshold = 1.0f;

    private const float speedRewardFactor = 0.01f;
    private const float alignmentRewardFactor = 0.02f;
    private const float checkpointProgressFactor = 0.1f;
    private const float reversePenalty = -0.2f;
    private const float circlingPenalty = -0.2f;
    private const float wallProximityRewardFactor = 0.01f;

    private Vector3 lastPosition;
    private bool isCircling = false;
    private float progressCheckInterval = 5f;
    private float lastProgressCheckTime = 0f;
    private float lastCheckpointProgress = 0f;
    private float maxTrackProgress = 0f;
    private int lastCheckpointIndex = -1;

    private const float optimalWallDistance = 1.0f;
    private const float minSafeWallDistance = 0.3f;
    private const float maxWallRayDistance = 2.5f;
    private const int wallRayCount = 6;
    private float globalMaxTrackProgress = 0f;

    // New speed configuration: set based on actual vehicle tests
    [Tooltip("Measured maximum speed of the vehicle (m/s)")]
    public float observedMaxSpeed = 1f;
    [Tooltip("Fraction of max speed to use as optimal cruise speed")]
    [Range(0.1f, 1f)]
    public float optimalSpeedMultiplier = 0.7f;
    
    // Phase 2 Cofiguration
    public float timePenaltyFactor = 0.001f;
    // How harshly to punish braking on a straight
    public float brakePenaltyFactor = 0.005f;
    // Only penalize brakes when forward speed > this
    private const float brakeSpeedThreshold = 1f;
    // Only penalize if brake input > this
    private const float brakeInputThreshold = 0.1f;

    private float spawnOffsetT;

    public RewardSystem(CarAgent agent, TrainingManager trainingManager)
    {
        this.agent = agent;
        this.trainingManager = trainingManager;
        debugger = agent.GetComponentInChildren<RewardDebugger>();
         int total = trainingManager.GetCheckpointCount();
        spawnOffsetT = total > 0 
        ? (float)agent.GetSpawnCheckpointIndex() / total 
        : 0f;
        Reset();
    }

    //reset car logic, handles "zeroing" out for next episode
    public void Reset()
    {
        Transform nextCheckpoint = trainingManager.GetCheckpoint(agent.GetCurrentCheckpointIndex());
        lastDistanceToCheckpoint = nextCheckpoint != null
            ? Vector3.Distance(agent.transform.position, nextCheckpoint.position)
            : float.MaxValue;

        lastPosition = agent.transform.position;
        lastCheckpointProgress = lastDistanceToCheckpoint;
        lastProgressCheckTime = Time.time;

        maxTrackProgress = spawnOffsetT;
        globalMaxTrackProgress = spawnOffsetT;
        isCircling = false;
        timeStationary = 0f;
        lastCheckpointIndex = -1;
        observedMaxSpeed = 1f;
    }

    public float CalculateReward()
    {
        float reward = 0f;
        Rigidbody rb = agent.GetComponent<Rigidbody>();
        Vector3 velocity = rb.linearVelocity;
        float speed = velocity.magnitude;

                // Update observed max speed dynamically
        observedMaxSpeed = Mathf.Max(observedMaxSpeed, speed);

        // Determine optimal cruise target at each tick
        float optimalSpeed = optimalSpeedMultiplier * observedMaxSpeed;

        // Phase 2: lap 0 = phase 1 | lap 1 = 0.5 | lap 2+ = 1.0 full weight
        float phaseWeight = 0f;
        if (agent.lapsCompleted >= 2) phaseWeight = 1f;
        else if (agent.lapsCompleted == 1) phaseWeight = 0.5f;

        if (phaseWeight > 0f)
        {
            // scale by fixedDeltaTime 
            reward -= phaseWeight * timePenaltyFactor * Time.fixedDeltaTime;
        }

        // Penalize standing still
        if (speed < minSpeedThreshold)
        {
            timeStationary += Time.fixedDeltaTime;
            if (timeStationary > stationaryTimeout)
                reward += -0.05f;
        }
        else timeStationary = 0f;

        // Penalize moving backwards
        float forwardSpeed = Vector3.Dot(agent.transform.forward, velocity);
        if (forwardSpeed < 0f)
        {
            float reverseDuration = Time.fixedDeltaTime;
            float extraPenalty = reverseDuration > 1f ? reversePenalty * 2f : reversePenalty;
            reward += extraPenalty * Time.fixedDeltaTime;
            if (debugger != null) debugger.reversePenalty = extraPenalty;
            
        }
        else
        {
            // float optimalSpeed = optimalSpeedMultiplier * measuredMaxSpeed;
            // float optimalSpeed = 7f; // Based on ~70% of typical max
            float ratio = Mathf.Clamp01(speed / optimalSpeed);
            float speedRwd = ratio * (2.0f - ratio) * speedRewardFactor * Time.fixedDeltaTime;
            float forwardBonus = Mathf.Max(0f, forwardSpeed) * 0.001f * Time.fixedDeltaTime;
            reward += speedRwd;
            reward += forwardBonus;
            if (debugger != null) debugger.speedReward = speedRwd;
            // Debug.Log($"OS: {speedRwd}");
        }

        // Progress along track (perpendicular progress)
        // SplineContainer splineContainer = trainingManager.trackGenerator.splineContainer;
        // if (splineContainer != null && splineContainer.Splines.Count > 0)
        // {
        //     var spline = splineContainer.Splines[trainingManager.trackGenerator.splineIndex];
        //     float3 worldPos = agent.transform.position;

        //     float3 nearestPoint;
        //     float t;

        //     SplineUtility.GetNearestPoint(spline, worldPos, out nearestPoint, out t);

        //     // Check if agent is mostly facing in the direction of movement
        //     Vector3 toNearest = (Vector3)nearestPoint - agent.transform.position;
        //     float alignmentToSpline = Vector3.Dot(agent.transform.forward, toNearest.normalized);

        //     float relT = (t - spawnOffsetT + 1f) % 1f;

        //     if (relT > maxTrackProgress && alignmentToSpline > 0.5f)
        //     {
        //         float rewardGain = (relT - maxTrackProgress) * 10f; // Tune multiplier for impact
        //         reward += rewardGain;
        //         maxTrackProgress = relT;

        //         // Optional: bonus for global exploration beyond past best
        //          if (relT > globalMaxTrackProgress)
        //         {
        //             reward += 1.0f;
        //             globalMaxTrackProgress = relT;
        //         }

        //         if (debugger != null)
        //         {
        //             debugger.projectedDistance = relT;
        //             debugger.MAXprojectedDistance = maxTrackProgress;
        //             debugger.GlobalprojectedDistance = globalMaxTrackProgress;
        //         }

        //         // Debug.Log($"[Spline Progress] t: {t:F3}, reward gain: {rewardGain:F4}");
        //     }
        // }

        // Direction allignment and Progress to checkpoint
        Transform nextCheckpoint = trainingManager.GetCheckpoint(agent.GetCurrentCheckpointIndex());
        if (nextCheckpoint != null)
        {
            float currDist = Vector3.Distance(agent.transform.position, nextCheckpoint.position);
            if (currDist < lastDistanceToCheckpoint)
                reward += (lastDistanceToCheckpoint - currDist) * checkpointProgressFactor;
            lastDistanceToCheckpoint = currDist;

            // Alignment
            Vector3 dir = (nextCheckpoint.position - agent.transform.position).normalized;
            float alignment = Vector3.Dot(agent.transform.forward, dir);
            if (alignment > 0)
            {
                // float decay = Mathf.Exp(-agent.TimeSinceLastCheckpoint / 30f);
                float alignmentBoost = Mathf.Pow(alignment, 2) * alignmentRewardFactor * Time.fixedDeltaTime; // *1.5 for Boosted factor
                reward +=  alignmentBoost;
                if (debugger != null) debugger.alignmentReward = alignmentRewardFactor;
                // Debug.Log($"Allignment: {alignmentRewardFactor}");
            }

        }

        // Circling detection
        if (Time.time - lastProgressCheckTime > progressCheckInterval)
        {
            float traveled = Vector3.Distance(agent.transform.position, lastPosition);
            float progressDelta = lastCheckpointProgress - lastDistanceToCheckpoint;
            float efficiency = Mathf.Abs(progressDelta) / (traveled + 0.01f);
            if (traveled > 10f && efficiency < 0.1f)
            {
                reward += circlingPenalty;
                isCircling = true;
                if (debugger != null) debugger.circlingPenalty = circlingPenalty;

            }
            else isCircling = false;
            lastProgressCheckTime = Time.time;
            lastCheckpointProgress = lastDistanceToCheckpoint;
            lastPosition = agent.transform.position;
        }

        // Wall proximity reward
        // float wallRewardMultiplier = Mathf.Clamp01((Academy.Instance.TotalStepCount - 200000f) / 200000f);
        // if (wallRewardMultiplier > 0f)
        // {
        //     for (int i = 0; i < wallRayCount; i++)
        //     {
        //         float angle = -60f + (i * (120f / (wallRayCount - 1)));
        //         Vector3 dir = Quaternion.Euler(0, angle, 0) * agent.transform.forward;
        //         if (Physics.Raycast(agent.transform.position + Vector3.up * 0.5f, dir, out RaycastHit hit, maxWallRayDistance))
        //         {
        //             if (hit.collider.CompareTag("Wall"))
        //             {
        //                 float deviation = Mathf.Abs(hit.distance - optimalWallDistance);
        //                 float proximityReward = Mathf.Max(0f, 1f - (deviation / optimalWallDistance)) * wallProximityRewardFactor;
        //                 reward += proximityReward* wallRewardMultiplier;
        //                 if (debugger != null) debugger.wallProximity = proximityReward;
        //                 // Debug.Log($"PR: {proximityReward}");

        //             }
        //         }
        //     }
        // }

        if (phaseWeight > 0f)
        {
            // Read the actual brake input from CarController
            var ctrl = agent.GetComponent<CarController>();
            float brakeInput = ctrl.CurrentBrake;

            if (forwardSpeed > brakeSpeedThreshold && brakeInput > brakeInputThreshold)
            {
                float penalty = phaseWeight
                              * brakePenaltyFactor
                              * brakeInput
                              * Time.fixedDeltaTime;
                reward -= penalty;
                if (debugger != null) debugger.reversePenalty = penalty; 
            }
        }

        if (debugger != null) debugger.total = reward;
        // Debug.Log($"reward: {reward}");
        return reward;
    }

    public float GetCheckpointReward()
    {
        int checkpointIndex = agent.GetCurrentCheckpointIndex();
        if (checkpointIndex != lastCheckpointIndex)
        {
            lastCheckpointIndex = checkpointIndex;
            float checkpointReward = 1.0f + 0.05f * checkpointIndex;
            if (debugger != null) debugger.checkpointProgress =  checkpointReward;

            return checkpointReward; // Scaling checkpoint reward
        }
        return 0f;
    }
    public float GetLapCompletionReward() => 10.0f;
    public float GetCollisionPenalty() => -1.0f;
    public float GetWrongCheckpointPenalty() => -1.0f;

    
}