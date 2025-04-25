using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using VehicleBehaviour;



public class CarAgent : Agent
{
    private WheelVehicle VehicleController;
    private CarController CarController;

    [HideInInspector]
    public TrainingManager trainingManager;
    private RewardSystem rewardSystem;
    private int currentCheckpointIndex = 0;
    private int maxCheckpointIndexAchieved = 0;
    private bool autoExplorationTriggered = false;
//autoExplorationTriggered



    
    [Header("Observation Settings")]
    public bool useRayPerceptionSensor = false;
    public float raycastDistance = 10f;
    public LayerMask raycastLayer;
    
    [Header("Debug Settings")]
    public bool logMetrics = false;

    private bool manuallyForcedExplore = false;

private int spawnCheckpointIndex = 0;
public void SetSpawnCheckpointIndex(int index)
{
    spawnCheckpointIndex = index;
}
public int GetSpawnCheckpointIndex() => spawnCheckpointIndex;


    public enum ControlMode
    {
        MLAgent,
        Heuristic,
        Manual
    }

    public ControlMode controlMode = ControlMode.MLAgent;

    private float timeSinceLastCheckpoint = 0f;
    private float stuckThreshold = 180f; // 3 minutes
    private bool isExploringMode = false;

    private float epsilon => Mathf.Lerp(0.5f, 0.05f, Academy.Instance.TotalStepCount / 1_000_000f);
    public float TimeSinceLastCheckpoint => timeSinceLastCheckpoint;

    
    public override void Initialize()
    {
        base.Initialize();
        VehicleController = GetComponent<WheelVehicle>();
        CarController = GetComponent<CarController>();

        if (VehicleController  == null || CarController == null)
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
        if (spawnCheckpointIndex == 0)
        {
            ResetCar();
        }
        else
        {
            RespawnAtRandomCheckpoint();
        }
        // trainingManager?.ResetCheckpoints();
        rewardSystem.Reset();
    }
    
    private void ResetCar()
    {
        if (VehicleController != null)
            VehicleController.ResetPos();
    }

    public void RespawnAtRandomCheckpoint()
{
    int total = trainingManager.GetCheckpointCount();
    int newIndex = Random.Range(1, total);

    SetSpawnCheckpointIndex(newIndex);

    Transform spawnPoint = trainingManager.GetCheckpoint(newIndex);
    Vector3 spawnPos = spawnPoint.position - spawnPoint.forward * 5f + Vector3.up * 0.5f;
    transform.SetPositionAndRotation(spawnPos, spawnPoint.rotation);

    currentCheckpointIndex = newIndex;
    maxCheckpointIndexAchieved = newIndex;
    timeSinceLastCheckpoint = 0f;
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

    // #if UNITY_EDITOR
    //             Debug.DrawRay(origin, dir * raycastDistance, hitInfo.collider ? Color.red : Color.green, 0.1f);
    // #endif
            }
        }


    }
    
    public override void OnActionReceived(ActionBuffers actions)

    {
        timeSinceLastCheckpoint += Time.fixedDeltaTime;

        if (Input.GetKeyDown(KeyCode.E))
        {
            manuallyForcedExplore = true;
            isExploringMode = true;
            Debug.Log($"time={timeSinceLastCheckpoint:F1}, cp={currentCheckpointIndex}, max={maxCheckpointIndexAchieved}");
            Debug.Log("[Manual] Exploration mode forced.");
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            manuallyForcedExplore = false;
            isExploringMode = false;
            autoExplorationTriggered = false;
            Debug.Log("[Manual] Exploration mode reset.");
        }

        
        bool nearMaxCheckpoint = currentCheckpointIndex >= maxCheckpointIndexAchieved - 2;
        bool trainingMatureEnough = Academy.Instance.TotalStepCount > 100000;
        // Debug.Log($"[CHECK] time={timeSinceLastCheckpoint:F1}, cp={currentCheckpointIndex}, max={maxCheckpointIndexAchieved}");
        if (timeSinceLastCheckpoint > stuckThreshold && nearMaxCheckpoint && trainingMatureEnough)
        {
            
            // Debug.Log("*****************EXPLORATION MODE*******************");
            if (!autoExplorationTriggered)
            {
                Debug.Log("[ExplorationMode] Agent entered exploration mode due to stagnation.");
                Debug.Log($"time={timeSinceLastCheckpoint:F1}, cp={currentCheckpointIndex}, max={maxCheckpointIndexAchieved}");
                autoExplorationTriggered = true;
            }
        }
        else
        {
            autoExplorationTriggered = false;
        }
        isExploringMode = manuallyForcedExplore || autoExplorationTriggered;
        //  Debug.Log($"[OnActionReceived] Agent received actions at time: {Time.time}");
        // Only apply actions if the car is under ML control
        if (!VehicleController.IsPlayer)
        {
            float steer = actions.ContinuousActions[0];
            // lastSteer = steer; // Track for smoothness penalty
            float throttle = actions.ContinuousActions[1];
            float brake = actions.ContinuousActions[2];

            // Hybrid exploration: epsilon-greedy and stuck mode
            if (isExploringMode )
            {
                // Debug.Log("randomsteps taken");
                // steer += Random.Range(-0.6f, 0.6f);
                // throttle += Random.Range(-0.3f, 0.3f);
                // steer = Random.Range(-1f, 1f);       // full overwrite
                // throttle = Random.Range(0.3f, 1f);   // prefer forward motion
                // brake = Random.Range(0f, 0.2f);

                bool hardTurn = Random.value < 0.5f;
                steer = hardTurn
                    ? Random.value < 0.5f ? Random.Range(-1f, -0.7f) : Random.Range(0.7f, 1f)
                    : Random.Range(-1f, 1f);

                throttle = Random.Range(0.3f, 1f);
                brake = Random.Range(0f, 0.2f);
            }
            else if (Random.value < epsilon && Academy.Instance.TotalStepCount > 50000)
            {
                // Gentle noise during normal training
                // Debug.Log("randomsteps taken");
                steer += Random.Range(-0.1f, 0.1f);
                throttle += Random.Range(0, 0.2f);
                throttle = Mathf.Clamp(throttle, 0f, 1f);
                steer = Mathf.Clamp(steer, -1f, 1f);

            }
            // Debug.Log($"[MLAction] Steer: {steer:F2}, Throttle: {throttle:F2}, Brake: {brake:F2}");

            // Apply actions to the Arcade Car Physics controller
            // VehicleController.SetSteering(steer);
            // VehicleController.SetThrottle(throttle);
            // VehicleController.SetBrake(brake);
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
        // continuousActions[0] = 0.5f;
        // continuousActions[1] = 0.5f;
        // continuousActions[2] = 0.0f;
    }
    
    public void OnCheckpointPassed(int checkpointIndex)
    {
        Debug.Log($"[Agent] Hit checkpoint: {checkpointIndex}");
        
        if (checkpointIndex > maxCheckpointIndexAchieved)
        {
            maxCheckpointIndexAchieved = checkpointIndex;
            timeSinceLastCheckpoint = 0f;
            isExploringMode = false;
        }

        // Debug.Log("TEST");
        if (checkpointIndex == currentCheckpointIndex)
        {
            AddReward(rewardSystem.GetCheckpointReward());
            Debug.Log("CP reward added");
            currentCheckpointIndex = (currentCheckpointIndex + 1) % trainingManager.GetCheckpointCount();
            
            if (currentCheckpointIndex == 0)
            {
                AddReward(rewardSystem.GetLapCompletionReward());
                EndEpisode();
            }
        }
        else
        {
            AddReward(rewardSystem.GetWrongCheckpointPenalty());
        }
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
}