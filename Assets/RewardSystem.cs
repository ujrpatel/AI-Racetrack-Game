using UnityEngine;

public class RewardSystem
{
    private CarAgent agent;
    private TrainingManager trainingManager;
    private float lastDistanceToCheckpoint;

    public RewardSystem(CarAgent agent, TrainingManager trainingManager)
    {
        this.agent = agent;
        this.trainingManager = trainingManager;
        Reset();
    }

    public void Reset()
    {
        Transform nextCheckpoint = trainingManager.GetCheckpoint(0);
        lastDistanceToCheckpoint = nextCheckpoint != null
            ? Vector3.Distance(agent.transform.position, nextCheckpoint.position)
            : float.MaxValue;
    }

    public float CalculateReward()
    {
        float reward = 0f;

        Transform nextCheckpoint = trainingManager.GetCheckpoint(agent.GetCurrentCheckpointIndex());

        if (nextCheckpoint != null)
        {
            float currentDistance = Vector3.Distance(agent.transform.position, nextCheckpoint.position);
            if (currentDistance < lastDistanceToCheckpoint)
            {
                reward += 0.01f * (lastDistanceToCheckpoint - currentDistance);
            }
            lastDistanceToCheckpoint = currentDistance;
        }

        float speed = agent.GetComponent<Rigidbody>().linearVelocity.magnitude;
        reward += speed * 0.001f;

        if (speed < 1f)
        {
            reward -= 0.01f;
        }

        return reward;
    }

    public float GetCheckpointReward()
    {
        return 1.0f;
    }

    public float GetLapCompletionReward()
    {
        return 10.0f;
    }

    public float GetCollisionPenalty()
    {
        return -1.0f;
    }

    public float GetWrongCheckpointPenalty()
    {
        return -1.0f;
    }
}