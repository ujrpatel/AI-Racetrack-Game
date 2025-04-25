using UnityEngine;
using System.Collections.Generic;
using Unity.MLAgents;

public class TrainingManager : MonoBehaviour
{
    [Header("References")]
    public SplineTrackGenerator trackGenerator;
    public GameObject carPrefab;

    private List<Transform> checkpoints;
    private List<GameObject> carInstances = new List<GameObject>();
    public List<CarAgent> carAgents = new List<CarAgent>();
    // private GameObject carInstance;

    [Header("Agent Settings")]
    [Range(1, 5)] public int numberOfAgents = 1; //  Number of agents to spawn
    public float spawnSpacing = 2.0f; //  Offset between spawned cars

    void Awake()
    {

        if (trackGenerator == null)
        {
            trackGenerator = FindFirstObjectByType<SplineTrackGenerator>();
            if (trackGenerator == null)
            {
                Debug.LogError("TrainingManager: SplineTrackGenerator not found!");
                return;
            }
        }

        checkpoints = trackGenerator.GetCheckpoints();
         if (checkpoints == null || checkpoints.Count == 0)
        {
            Debug.LogError("[Training Manager]: No checkpoints found!");
            return;
        }

        // SpawnAgent(0,0);

        // for (int i = 1; i < numberOfAgents; i++)
        // {
        //     int randomCheckpoint = Random.Range(1, checkpoints.Count); // Start from 1 to avoid index 0
        //     SpawnAgent(randomCheckpoint, i);
        // }
        // for (int i = 1; i < numberOfAgents; i++)
        // {
        //     int randomCheckpoint;
        //     do
        //     {
        //         randomCheckpoint = Random.Range(0, checkpoints.Count); // Could be any checkpoint
        //     }
        //     while (randomCheckpoint == 0); // Ensure we don't duplicate the agent at checkpoint 0

        //     SpawnAgent(randomCheckpoint, i);
        // }
        for (int i = 0; i < numberOfAgents; i++)
        {
            int checkpointIndex = (i == 0) ? 0 : Random.Range(1, checkpoints.Count);
            SpawnAgent(checkpointIndex, i);
        }

    }

    void SpawnAgent(int checkpointIndex, int agentNumber) {

        Transform spawnPoint = GetCheckpoint(checkpointIndex);
        if (spawnPoint == null) {
            Debug.LogError($"[TrainingManager]: Invalid checkpoint index: {checkpointIndex}");
            return;
        }

        Vector3 offset = spawnPoint.right * spawnSpacing * agentNumber;
        // Spawn the car 5 units before the checkpoint to trigger the lap timer
        Vector3 spawnPosition = spawnPoint.position - (spawnPoint.forward * 5f) + (Vector3.up * 0.5f + offset);
        Quaternion spawnRotation = spawnPoint.rotation;
        // Debug.Log($"[TrainingManager] Spawning agent at position: {spawnPosition}");

        GameObject car = Instantiate(carPrefab, spawnPosition, spawnRotation);
        carInstances.Add(car);

        // Attach the camera only to the first agent
        if (agentNumber == 0)
        {
            FollowCamera cam = FindFirstObjectByType<FollowCamera>();
            if (cam != null)
            {
                cam.target = car.transform;
            }
        }

        CarAgent carAgent = car.GetComponent<CarAgent>();
        if (carAgent != null) {
            carAgent.SetSpawnCheckpointIndex(checkpointIndex);
            carAgent.Initialize();
            carAgent.trainingManager = this; // expose reference
            carAgents.Add(carAgent);
        }
    }

    public Transform GetCheckpoint(int index)
    {
        if (checkpoints == null || index < 0 || index >= checkpoints.Count)
        {
            return null;
        }
        return checkpoints[index];
    }

    public int GetCheckpointCount()
    {
        return checkpoints != null ? checkpoints.Count : 0;
    }

    // Reset all checkpoint triggers
    public void ResetCheckpoints()
    {
        foreach (Transform checkpoint in checkpoints)
        {
            Checkpoint cp = checkpoint.GetComponent<Checkpoint>();
            if (cp != null)
            {
                Debug.Log("SHOuldnt be triggered");// cp.ResetCheckpoint();

            }
        }
    }
}