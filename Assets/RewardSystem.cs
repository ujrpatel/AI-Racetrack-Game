// Updated RewardSystem.cs
using UnityEngine;

public class RewardSystem
{
    private CarAgent agent;
    private TrainingManager trainingManager;
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

    private const float optimalWallDistance = 1.0f;
    private const float minSafeWallDistance = 0.3f;
    private const float maxWallRayDistance = 2.5f;
    private const int wallRayCount = 6;

    public RewardSystem(CarAgent agent, TrainingManager trainingManager)
    {
        this.agent = agent;
        this.trainingManager = trainingManager;
        Reset();
    }

    public void Reset()
    {
        Transform nextCheckpoint = trainingManager.GetCheckpoint(agent.GetCurrentCheckpointIndex());
        lastDistanceToCheckpoint = nextCheckpoint != null ? Vector3.Distance(agent.transform.position, nextCheckpoint.position) : float.MaxValue;
        lastPosition = agent.transform.position;
        lastCheckpointProgress = lastDistanceToCheckpoint;
        lastProgressCheckTime = Time.time;
        isCircling = false;
        timeStationary = 0f;
    }

    public float CalculateReward()
    {
        float reward = 0f;
        Rigidbody rb = agent.GetComponent<Rigidbody>();
        Vector3 velocity = rb.linearVelocity;
        float speed = velocity.magnitude;

        // Penalize standing still
        if (speed < minSpeedThreshold)
        {
            timeStationary += Time.fixedDeltaTime;
            if (timeStationary > stationaryTimeout)
                reward += -0.1f;
        }
        else timeStationary = 0f;

        // Penalize moving backwards
        float forwardSpeed = Vector3.Dot(agent.transform.forward, velocity);
        if (forwardSpeed < 0f)
            reward += reversePenalty * Time.fixedDeltaTime;
        else
        {
            float optimalSpeed = 7f; // Based on ~70% of typical max
            float ratio = Mathf.Clamp01(speed / optimalSpeed);
            float speedRwd = ratio * (2.0f - ratio) * speedRewardFactor * Time.fixedDeltaTime;
            reward += speedRwd;
        }

        // Progress to checkpoint
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
                reward += Mathf.Pow(alignment, 2) * alignmentRewardFactor * Time.fixedDeltaTime;
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
            }
            else isCircling = false;
            lastProgressCheckTime = Time.time;
            lastCheckpointProgress = lastDistanceToCheckpoint;
            lastPosition = agent.transform.position;
        }

        // Wall proximity reward
        for (int i = 0; i < wallRayCount; i++)
        {
            float angle = -60f + (i * (120f / (wallRayCount - 1)));
            Vector3 dir = Quaternion.Euler(0, angle, 0) * agent.transform.forward;
            if (Physics.Raycast(agent.transform.position + Vector3.up * 0.5f, dir, out RaycastHit hit, maxWallRayDistance))
            {
                if (hit.collider.CompareTag("Wall"))
                {
                    float deviation = Mathf.Abs(hit.distance - optimalWallDistance);
                    float proximityReward = Mathf.Max(0f, 1f - (deviation / optimalWallDistance)) * wallProximityRewardFactor;
                    reward += proximityReward;
                }
            }
        }

        return reward;
    }

    public float GetCheckpointReward() => 1.0f;
    public float GetLapCompletionReward() => 10.0f;
    public float GetCollisionPenalty() => -1.0f;
    public float GetWrongCheckpointPenalty() => -1.0f;
}
