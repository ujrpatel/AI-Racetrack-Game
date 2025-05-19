using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using VehicleBehaviour;
using Unity.Collections.LowLevel.Unsafe;
using System.Collections.Generic;
using JetBrains.Annotations;




public class CarAgent : Agent
{
    private WheelVehicle VehicleController;
    private CarController CarController;
    [HideInInspector] public TrainingManager trainingManager;
    private RewardSystem rewardSystem;


    // Lap timing system
    [HideInInspector] public bool isMainAgent = false;
    private float lapStartTime;
    [HideInInspector] public float currentLapTime = 0f;
    [HideInInspector] public float lastLapTime    = 0f;
    [HideInInspector] public float bestLapTime    = float.MaxValue;
    [HideInInspector] public int   lapsCompleted  = 0;

    private float lastResetTime;

    
    [Header("Observation Settings")]
    public bool useRayPerceptionSensor = false;
    public float raycastDistance = 10f;
    public LayerMask raycastLayer;


    [Header("Debug Settings")]
    public bool logMetrics = false;


    // Checkpoint system and exploration mode 
    private int currentCheckpointIndex = 0;
    private int spawnCheckpointIndex = 0;
    public void SetSpawnCheckpointIndex(int index)
    {
        spawnCheckpointIndex = index;
    }
    public int GetSpawnCheckpointIndex() => spawnCheckpointIndex;


    // Auto reset fields
    [Header("Auto-Reset Settings")]
    [Tooltip("Max tilt (degrees) before we give up and respawn")]
    public float maxTiltAngle = 60f;

    [Tooltip("Distance to raycast down to detect the road")]
    public float offTrackRayDistance = 1f;
    [Tooltip("Which layers count as 'road'")]
    public LayerMask roadLayer;

    [Header("Auto-Reset Settings (Extended)")]
    [Tooltip("Seconds to wait after spawn before checking ground/contact")]
    public float groundCheckDelay = 1f;
    private float _spawnTime;

    public enum ControlMode
    {
        MLAgent,
        Heuristic,
        Manual
    }

    public ControlMode controlMode = ControlMode.MLAgent;


    public override void Initialize()
    {
        base.Initialize();
        VehicleController = GetComponent<WheelVehicle>();
        CarController = GetComponent<CarController>();
        // dynamically selects the control mode for all 3 modes, if MLA is enablled then the use liner steering overide is called

        if (VehicleController == null || CarController == null)
        {
            Debug.LogError("[CarAgent] Missing VehicleController or CarController component!");
            return;
        }
        switch (controlMode)
        {
            case ControlMode.MLAgent:
                VehicleController.IsPlayer = false;  // disable player input
                Debug.Log($"[CONTROL] ML AGents");
                break;

            case ControlMode.Heuristic:
                VehicleController.IsPlayer = false; 
                Debug.Log($"[CONTROL] HEURIStic");
                break;
            case ControlMode.Manual:
                VehicleController.IsPlayer = true;   // allow manual input
                Debug.Log($"[CONTROL] ManUal");
                break;
        }

        trainingManager = FindFirstObjectByType<TrainingManager>();
        if (trainingManager == null)
        {
            Debug.LogWarning("No TrainingManager found in the scene. Some functionality may be limited.", this);
        }
        rewardSystem = new RewardSystem(this, trainingManager);
        if (controlMode != ControlMode.Manual && VehicleController is WheelVehicle wheel)
        {
            wheel.LinearSteeringOverride = true;
        }
    }
    
    public override void OnEpisodeBegin()
    {
        _spawnTime = Time.time;
        lastResetTime = Time.time;

        if (isMainAgent)
        {
            ResetCar();
            currentCheckpointIndex = 1;  // next we expect CP 1 (used for timing and respawn logic)
            lastLapTime = 0f;
            bestLapTime = float.MaxValue;
            lapStartTime = Time.time;
        }
        else
        {
            spawnCheckpointIndex = 0;
            RespawnAtRandomCheckpoint();
        }

        rewardSystem.Reset();
        RaceEvents.LapStarted(gameObject, 1);
    }
    
    private void ResetCar()
    {
        if (VehicleController != null)
            VehicleController.ResetPos();
    }

    // first spawn is handled in the training manager, in game they are handlled here
    public void RespawnAtRandomCheckpoint()
    {
        int total = trainingManager.GetCheckpointCount();
        int newIndex;

        if (spawnCheckpointIndex != 0)
        {
            newIndex = spawnCheckpointIndex;
        }
        else
        {
            newIndex = Random.Range(1, total);
        }

        spawnCheckpointIndex = newIndex;

        Transform spawnPoint = trainingManager.GetCheckpoint(newIndex);
        Vector3 spawnPos = spawnPoint.position - spawnPoint.forward * 5f + Vector3.up * 0.5f;
        transform.SetPositionAndRotation(spawnPos, spawnPoint.rotation);

        currentCheckpointIndex = (newIndex + 1) % total;
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // Debug.Log($"[CollectObservations] Called at time: {Time.time}");
        // Car velocity (normalized)
        sensor.AddObservation(GetComponent<Rigidbody>().linearVelocity.normalized);

        // Direction to next checkpoint
        Transform nextCheckpoint = trainingManager.GetCheckpoint(currentCheckpointIndex);

        if (nextCheckpoint != null)
        {
            Vector3 directionToCheckpoint = (nextCheckpoint.position - transform.position).normalized;
            sensor.AddObservation(directionToCheckpoint);

            float distanceToCheckpoint = Vector3.Distance(transform.position, nextCheckpoint.position);
            sensor.AddObservation(distanceToCheckpoint / 100f);
        }
        else
        {
            sensor.AddObservation(Vector3.zero);
            sensor.AddObservation(1f);  
        }



        int checkpointIndex = currentCheckpointIndex;
        int totalCheckpoints = trainingManager.GetCheckpointCount();
        sensor.AddObservation((float)checkpointIndex / totalCheckpoints); // normalized

        bool checkpointSeenThisFrame = false;

        // Custom raycasts
        if (!useRayPerceptionSensor)
        {
            Vector3 origin = transform.position + Vector3.up * 0.5f;
            Vector3[] rayDirections = new Vector3[]
            {
                Quaternion.Euler(0, -110, 0) * transform.forward,
                Quaternion.Euler(0, -75, 0) * transform.forward,
                Quaternion.Euler(0, -45, 0) * transform.forward,
                Quaternion.Euler(0, -20, 0) * transform.forward,
                transform.forward,
                Quaternion.Euler(0, 20, 0) * transform.forward,
                Quaternion.Euler(0, 45, 0) * transform.forward,
                Quaternion.Euler(0, 75, 0) * transform.forward,
                Quaternion.Euler(0, 110, 0) * transform.forward,
                -transform.forward // to detect reversing
            };

            foreach (Vector3 dir in rayDirections)
            {
                float distance = 1f;
                float objectType = 0f; // 0 = nothing, 1 = wall, 2 = checkpoint

                if (Physics.Raycast(origin, dir, out RaycastHit hitInfo, raycastDistance, raycastLayer))
                {
                    distance = hitInfo.distance / raycastDistance;

                    // Detect object type by tag
                    if (hitInfo.collider.CompareTag("Wall"))
                        objectType = 1f;
                    else if (hitInfo.collider.CompareTag("Checkpoint") || hitInfo.collider.CompareTag("StartFinish"))
                        objectType = 2f;
                        if (objectType == 2f && !checkpointSeenThisFrame)
                        {
                            AddReward(0.01f);
                            checkpointSeenThisFrame = true;
                        }

#if UNITY_EDITOR
                    Color rayColor = objectType == 1f ? Color.red : (objectType == 2f ? Color.green : Color.blue);
                    Debug.DrawRay(origin, dir * hitInfo.distance, rayColor, 0.1f);
#endif
                }
                else
                {
#if UNITY_EDITOR
                    Debug.DrawRay(origin, dir * raycastDistance, Color.gray, 0.1f);
#endif
                }

                // Add distance and object type to observations with One Hot encoding
                sensor.AddObservation(distance);                 // [0-1]
                sensor.AddObservation(objectType == 1f ? 1f : 0f); // IsWall
                sensor.AddObservation(objectType == 2f ? 1f : 0f); // IsCheckpoint

            }
        }


    }
    
    public override void OnActionReceived(ActionBuffers actions)
    {
        

        if (!VehicleController.IsPlayer)
        {
            float steer = actions.ContinuousActions[0];
            float throttle = actions.ContinuousActions[1];
            float brake = actions.ContinuousActions[2];

            CarController.ApplyControl(steer, throttle, brake);
        
        }
        Vector3 steerDir = Quaternion.Euler(0, actions.ContinuousActions[0] * 45f, 0) * transform.forward;
        Debug.DrawRay(transform.position + Vector3.up * 1f, steerDir * 5f, Color.cyan, 0.2f);
        //or steer for continous actions

        // Always calculate and apply reward, regardless of control mode
        AddReward(rewardSystem.CalculateReward());
    }
    
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<float> continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = 0.5f;
        continuousActions[1] = 0.5f;
        continuousActions[2] = 0.0f;
    }

    public void OnCheckpointPassed(int checkpointIndex)
    {
        if (Time.time - lastResetTime < 0.2f) return;

        if (isMainAgent && checkpointIndex == 0 && currentCheckpointIndex == 1)
        {
            RaceEvents.CheckpointPassed(gameObject,checkpointIndex);
            AddReward(rewardSystem.GetCheckpointReward());
            return;
        }

        if (!isMainAgent && checkpointIndex == spawnCheckpointIndex && currentCheckpointIndex == (spawnCheckpointIndex + 1) % trainingManager.GetCheckpointCount())
        {
            RaceEvents.CheckpointPassed(gameObject,checkpointIndex);
            AddReward(rewardSystem.GetCheckpointReward());
            return;
        }
        // Debug.Log($"[Agent] Hit checkpoint: {checkpointIndex}");

        if (checkpointIndex != currentCheckpointIndex)
        {

            Debug.Log("Car going backwards");
            AddReward(rewardSystem.GetWrongCheckpointPenalty());
            EndEpisode();
            return;
        }

        RaceEvents.CheckpointPassed(gameObject,checkpointIndex);
        AddReward(rewardSystem.GetCheckpointReward());

        if (currentCheckpointIndex == 0 && checkpointIndex == 0)
        {
            lapsCompleted++;
            AddReward(rewardSystem.GetLapCompletionReward());

            // calculate lap-time & speed
            float lapTime = Time.time - lapStartTime;
            lastLapTime   = lapTime;
            if (lapTime < bestLapTime) bestLapTime = lapTime;
            lapStartTime  = Time.time;

            // broadcast complete
            float trackLen = trainingManager.trackGenerator.estimatedTrackLength;
            float avgSpeed = trackLen / lapTime;
            // spawnCheckpointIndex = maxCheckpointIndexAchieved;
            RaceEvents.LapCompleted(gameObject, lapsCompleted, lapTime, avgSpeed);
            if (isMainAgent)
            {
                Debug.Log($"[MainAgent] Lap {lapsCompleted} time: {lapTime:F2}s");
            }

            EndEpisode();
            return;
        }
        currentCheckpointIndex = (currentCheckpointIndex + 1) % trainingManager.GetCheckpointCount();
        
    }
    
    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
        {
            AddReward(rewardSystem.GetCollisionPenalty());
            EndEpisode();
        }
    }
    
    public int GetCurrentCheckpointIndex()
    {
        return currentCheckpointIndex;
    }

    void FixedUpdate()
{
    // only start checking once we've ever been grounded
    if (Time.time - _spawnTime < groundCheckDelay)
        return;
    

    float tilt = Vector3.Angle(transform.up, Vector3.up);
    if (tilt > maxTiltAngle)
    {
        Debug.Log("[CAR AGENT] Car tilted... resetting");
        RespawnAtRandomCheckpoint();
        _spawnTime = Time.time;
        return;
    }

    Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
    // if wheels lose contact you’re off the track
    bool didHit = Physics.Raycast(rayOrigin, Vector3.down, out var hit, offTrackRayDistance);

    if (!didHit || !hit.collider.CompareTag("Road"))
    {
        Debug.Log("[CAR AGENT] Car clipped through track/ outside bounds ... resetting");
        RespawnAtRandomCheckpoint();
        _spawnTime = Time.time;
    }

    // else 
    // {
    //     // if we hit nothing, we’re off the track
    //     Debug.Log("[CAR AGENT] No ground detected – respawning");
    //     RespawnAtRandomCheckpoint();
    //     _spawnTime = Time.time;
    // }
}

}