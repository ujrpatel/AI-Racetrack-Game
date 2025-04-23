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

    private TrainingManager trainingManager;
    private RewardSystem rewardSystem;
    private int currentCheckpointIndex = 0;
    
    [Header("Observation Settings")]
    public bool useRayPerceptionSensor = false;
    public float raycastDistance = 10f;
    public LayerMask raycastLayer;
    
    [Header("Debug Settings")]
    public bool logMetrics = false;

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
        // if (controlMode != ControlMode.MLAgent && VehicleController is WheelVehicle wheel)
        // {
        //     wheel.LinearSteeringOverride = true;
        // }
    }
    
    public override void OnEpisodeBegin()
    {
        ResetCar();
        currentCheckpointIndex = 0;
        rewardSystem.Reset();
    }
    
    private void ResetCar()
    {
        if (VehicleController != null)
            VehicleController.ResetPos();
    }
    
    public override void CollectObservations(VectorSensor sensor)
    {
        Debug.Log($"[CollectObservations] Called at time: {Time.time}");
        // Car velocity (normalized)
        sensor.AddObservation(GetComponent<Rigidbody>().linearVelocity.normalized);

        // Direction to next checkpoint
        Transform nextCheckpoint = trainingManager.GetCheckpoint(currentCheckpointIndex);

        if (nextCheckpoint != null)
        {
            Vector3 directionToCheckpoint = (nextCheckpoint.position - transform.position).normalized;
            sensor.AddObservation(directionToCheckpoint);

        }
        else
        {
            sensor.AddObservation(Vector3.zero);
        }

        // Custom raycasts
        if (!useRayPerceptionSensor)
        {
            Vector3 origin = transform.position + Vector3.up * 0.5f;
            Vector3[] rayDirections = new Vector3[]
            {
                Quaternion.Euler(0, -75, 0) * transform.forward,
                Quaternion.Euler(0, -45, 0) * transform.forward,
                Quaternion.Euler(0, -20, 0) * transform.forward,
                transform.forward,
                Quaternion.Euler(0, 20, 0) * transform.forward,
                Quaternion.Euler(0, 45, 0) * transform.forward,
                Quaternion.Euler(0, 75, 0) * transform.forward,
                -transform.forward // to detect reversing
            };

            foreach (Vector3 dir in rayDirections)
            {
                bool hit = Physics.Raycast(origin, dir, out RaycastHit hitInfo, raycastDistance, raycastLayer);
                float normalizedDistance = hit ? hitInfo.distance / raycastDistance : 1f;
                sensor.AddObservation(normalizedDistance);

    #if UNITY_EDITOR
                Debug.DrawRay(origin, dir * raycastDistance, hit ? Color.red : Color.green, 0.1f);
    #endif
            }
        }

    }
    
    public override void OnActionReceived(ActionBuffers actions)

    {
         Debug.Log($"[OnActionReceived] Agent received actions at time: {Time.time}");
        // Only apply actions if the car is under ML control
        if (!VehicleController.IsPlayer)
        {
            float steer = actions.ContinuousActions[0];
            float throttle = actions.ContinuousActions[1];
            float brake = actions.ContinuousActions[2];
            Debug.Log($"[MLAction] Steer: {steer:F2}, Throttle: {throttle:F2}, Brake: {brake:F2}");

            // Apply actions to the Arcade Car Physics controller
            // VehicleController.SetSteering(steer);
            // VehicleController.SetThrottle(throttle);
            // VehicleController.SetBrake(brake);
            CarController.ApplyControl(steer, throttle, brake);

        
        }
        Vector3 steerDir = Quaternion.Euler(0, actions.ContinuousActions[0] * 45f, 0) * transform.forward;
        Debug.DrawRay(transform.position + Vector3.up * 1f, steerDir * 5f, Color.cyan, 0.2f);


        // Always calculate and apply reward, regardless of control mode
        AddReward(rewardSystem.CalculateReward());
    }
    
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<float> continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = 0.0f;
        continuousActions[1] = 0.1f;
        continuousActions[2] = 0.0f;
    }
    
    public void OnCheckpointPassed(int checkpointIndex)
    {
        if (checkpointIndex == currentCheckpointIndex)
        {
            AddReward(rewardSystem.GetCheckpointReward());
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