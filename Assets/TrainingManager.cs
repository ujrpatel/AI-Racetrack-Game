using UnityEngine;
using System.Collections.Generic;
using Unity.MLAgents;

public class TrainingManager : MonoBehaviour
{
    [Header("References")]
    public SplineTrackGenerator trackGenerator;
    public GameObject carPrefab;

    private List<Transform> checkpoints;
    private GameObject carInstance;

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
        SpawnAgent(0);
    }

    void SpawnAgent(int checkpointIndex) {
        if (carInstance != null) {
            Destroy(carInstance);
        }

        Transform spawnPoint = GetCheckpoint(checkpointIndex);
        if (spawnPoint == null) {
            Debug.LogError("TrainingManager: Invalid spawn checkpoint!");
            return;
        }

        // Spawn the car 5 units before the checkpoint to trigger the lap timer
        Vector3 spawnPosition = spawnPoint.position - (spawnPoint.forward * 5f) + (Vector3.up * 0.5f);
        Quaternion spawnRotation = spawnPoint.rotation;
        Debug.Log($"[TrainingManager] Spawning agent at position: {spawnPosition}");

        carInstance = Instantiate(carPrefab, spawnPosition, spawnRotation);
        CarAgent carAgent = carInstance.GetComponent<CarAgent>();
        if (carAgent != null) {
            carAgent.Initialize();
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
            CheckpointTrigger trigger = checkpoint.GetComponent<CheckpointTrigger>();
            if (trigger != null)
            {
                trigger.ResetTrigger();
            }
        }
    }
}