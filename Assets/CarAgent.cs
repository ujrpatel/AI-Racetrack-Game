using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Sensors;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Policies;
using VehicleBehaviour;



public class CarAgent : Agent
{
    private WheelVehicle carController;

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
        carController = GetComponent<WheelVehicle>();
        if (carController == null)
        {
            Debug.LogError("[CarAgent] Missing CarController component!");
            return;
        }
        switch (controlMode)
        {
            case ControlMode.MLAgent:
                carController.IsPlayer = false;  // disable player input
                Debug.Log($"[CONTROL] ML AGents");
                break;

            case ControlMode.Heuristic:
                Debug.Log($"[CONTROL] HEURIStic");
                break;
            case ControlMode.Manual:
                carController.IsPlayer = true;   // allow manual input
                Debug.Log($"[CONTROL] ManUal");
                break;
        }

        trainingManager = FindFirstObjectByType<TrainingManager>();
        if (trainingManager == null)
        {
            Debug.LogWarning("No TrainingManager found in the scene. Some functionality may be limited.", this);
        }
        rewardSystem = new RewardSystem(this, trainingManager);
    }
    
    public override void OnEpisodeBegin()
    {
        ResetCar();
        currentCheckpointIndex = 0;
        rewardSystem.Reset();
    }
    
    private void ResetCar()
    {
        if (carController != null)
            carController.ResetPos();
    }
    
    public override void CollectObservations(VectorSensor sensor)
    {
        Debug.Log($"[CollectObservations] Called at time: {Time.time}");
        // Car velocity (normalized)
        sensor.AddObservation(GetComponent<Rigidbody>().linearVelocity.normalized);
        // Debug.Log($"[CollectObservations] 2 Called at time: {Time.time}");
        // Direction to next checkpoint
        Transform nextCheckpoint = trainingManager.GetCheckpoint(currentCheckpointIndex);
        // Debug.Log($"[CollectObservations] 3 nC:{nextCheckpoint}");
        if (nextCheckpoint != null)
        {
            Vector3 directionToCheckpoint = (nextCheckpoint.position - transform.position).normalized;
            sensor.AddObservation(directionToCheckpoint);
            // Debug.Log($"[CollectObservations] directionme: {directionToCheckpoint}");
        }
        else
        {
            sensor.AddObservation(Vector3.zero);
        }
        // Debug.Log($"[CollectObservations] 4 Called at time: {Time.time}");
        // Custom raycasts
        if (!useRayPerceptionSensor)
        {
            // Debug.Log($"[CollectObservations] 5 Called at time: {Time.time}");
            Vector3[] rayDirections = { transform.forward, -transform.right, transform.right };
            foreach (Vector3 dir in rayDirections)
            {
                // Debug.Log($"[CollectObservations] 6 Called at time: {Time.time}");
                bool hit = Physics.Raycast(transform.position + Vector3.up * 0.5f, dir, out RaycastHit hitInfo, raycastDistance, raycastLayer);
                sensor.AddObservation(hit ? hitInfo.distance / raycastDistance : 1f);
                // Debug.Log($"[CollectObservations] 7b Called at time: { hitInfo.distance / raycastDistance : 1f}");
                // Debug.Log($"[CollectObservations] 6b Called at time: {Time.time}");
            }
            // Debug.Log($"[CollectObservations] 7 Called at time: {Time.time}");
        }
        // Debug.Log($"[CollectObservations] 8 Called at time: {Time.time}");
    }
    
    public override void OnActionReceived(ActionBuffers actions)

    {
         Debug.Log($"[OnActionReceived] Agent received actions at time: {Time.time}");
        // Only apply actions if the car is under ML control
        if (!carController.IsPlayer)
        {
            float steer = actions.ContinuousActions[0];
            float throttle = actions.ContinuousActions[1];
            float brake = actions.ContinuousActions[2];
            Debug.Log($"[MLAction] Steer: {steer:F2}, Throttle: {throttle:F2}, Brake: {brake:F2}");

            // Apply actions to the Arcade Car Physics controller
            carController.SetSteering(steer);
            carController.SetThrottle(throttle);
            carController.SetBrake(brake);

        
        }

        // Always calculate and apply reward, regardless of control mode
        AddReward(rewardSystem.CalculateReward());
    }
    
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        ActionSegment<float> continuousActions = actionsOut.ContinuousActions;
        continuousActions[0] = Input.GetAxis("Horizontal");
        continuousActions[1] = Mathf.Max(Input.GetAxis("Vertical"), 0f);
        continuousActions[2] = Input.GetAxis("Brake");
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