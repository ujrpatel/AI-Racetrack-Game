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
    [Range(1, 5)] public int numberOfAgents = 5; //  Number of agents to spawn
    public float spawnSpacing = 2.0f; //  Offset between spawned cars

    void Awake()
    {
        // checks to see if spline and checkpoints have been generated correctly
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

        for (int i = 0; i < numberOfAgents; i++)
        {
            int checkpointIndex = (i == 0) ? 0 : Random.Range(1, checkpoints.Count);
            SpawnAgent(checkpointIndex);
        }

    }

    void SpawnAgent(int checkpointIndex) {

        Transform spawnPoint = GetCheckpoint(checkpointIndex);
        if (spawnPoint == null) {
            Debug.LogError($"[TrainingManager]: Invalid checkpoint index: {checkpointIndex}");
            return;
        }

        // Small random rotation 5 degrees, to provide randomisation and generalisation
        float randomYaw = Random.Range(-5f, 5f);
        Quaternion spawnRotation = Quaternion.Euler(0f, spawnPoint.rotation.eulerAngles.y + randomYaw, 0f);

        // Small variations lateraly and linearlly across the checkpoints, for randomisation. different senarios
        Vector3 lateralJitter = spawnPoint.right * Random.Range(-0.5f, 0.5f);
        Vector3 forwardJitter = spawnPoint.forward * Random.Range(-1f, 1f);

        Vector3 spawnPosition = spawnPoint.position - (spawnPoint.forward * 3f) + lateralJitter + forwardJitter + (Vector3.up * 0.5f);
        
        // Debug.Log($"[TrainingManager] Spawning agent at position: {spawnPosition}");

        GameObject car = Instantiate(carPrefab, spawnPosition, spawnRotation);
        CarAgent carAgent = car.GetComponent<CarAgent>();
        carInstances.Add(car);
        
        // Attach the camera only to the first agent
        if (carInstances.Count == 1)
        {
            FollowCamera cam = FindFirstObjectByType<FollowCamera>();
            if (cam != null)
            {
                //traking the main car with camera and lapping system
                carAgent.isMainAgent = true;
                cam.target = car.transform;
            }
            else
            {
                carAgent.isMainAgent = false;
            }
        }

        if (carAgent != null) {

            carAgent.SetSpawnCheckpointIndex(checkpointIndex);
            carAgent.Initialize();
            carAgent.trainingManager = this; // exposed reference
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

    // Reset all cars incase of error
    public void ResetAllCars()
    {
        foreach (var agent in carAgents)
        {
            agent.EndEpisode();
        }
        Debug.Log("[Training Manager] All cars have been reset");
    }

}