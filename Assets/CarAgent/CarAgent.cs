using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Collections.Generic;

public class CarAgent : Agent
{
    public CarController carController;
    private Rigidbody rb;
    
    // Checkpoint tracking
    private List<Transform> checkpoints;
    private int nextCheckpointIndex = 0;
    private float timeSinceLastCheckpoint = 0f;
    public float maxTimeWithoutCheckpoint = 30f;
    
    // Performance tracking
    private float totalDistance = 0f;
    private Vector3 lastPosition;
    private int totalCheckpointsPassed = 0;
    private int totalLapsCompleted = 0;
    
    // Training parameters (can be evolved by genetic algorithm)
    public float speedRewardFactor = 0.01f;
    public float alignmentRewardFactor = 0.02f;
    public float checkpointReward = 1.0f;
    public float lapCompletionReward = 5.0f;
    public float wallCollisionPenalty = 1.0f;
    public float offTrackPenalty = 0.5f;
    public float timeoutPenalty = 0.5f;
    
    // Cumulative reward for this episode
    private float episodeReward = 0f;
    
    // Reference to track for reset
    public SplineTrackGenerator trackGenerator;
    
    // Debug settings
    public bool enableDebugLogging = true;
    
    // Reference to lap tracking system
    private LapTrackingSystem lapTrackingSystem;
    
    // Lap tracking data
    private float lastLapTime = 0f;
    private int currentLap = 0;
    private float bestLapTime = float.MaxValue;
    
    [Header("Exploration Settings")]
    public bool forceExploration = true;
    public float explorationThrottle = 0.8f;
    
    // Use Start instead of OnEnable to avoid access modifier issues
    public override void Initialize()
    {
        base.Initialize();
        
        // Initialize components
        carController = GetComponent<CarController>();
        rb = GetComponent<Rigidbody>();
        
        if (carController == null)
        {
            Debug.LogError("[CarAgent] CarController component missing! Adding one...");
            carController = gameObject.AddComponent<CarController>();
            // Set default values
            carController.maxSpeed = 50f;
            carController.acceleration = 2000f;
            carController.maxSteeringAngle = 30f;
            carController.brakingForce = 500f;
            carController.downforce = 50f;
        }
        
        if (rb == null)
        {
            Debug.LogError("[CarAgent] Rigidbody component missing!");
            rb = gameObject.AddComponent<Rigidbody>();
        }
        
        // Cache checkpoints if track generator is available
        if (trackGenerator != null)
        {
            checkpoints = trackGenerator.GetCheckpoints();
        }
    }
    
    public override void OnEpisodeBegin()
    {
        // Reset car position to start position
        if (trackGenerator != null && checkpoints != null && checkpoints.Count > 0)
        {
            transform.position = checkpoints[0].position;
            transform.rotation = checkpoints[0].rotation;
        }
        else
        {
            // Fallback if checkpoints aren't available
            if (trackGenerator != null)
            {
                // Try to get checkpoints again
                checkpoints = trackGenerator.GetCheckpoints();
                if (checkpoints != null && checkpoints.Count > 0)
                {
                    transform.position = checkpoints[0].position;
                    transform.rotation = checkpoints[0].rotation;
                }
                else
                {
                    // If still no checkpoints, use current position
                    Debug.LogWarning("[CarAgent] No checkpoints found for reset position");
                }
            }
        }
        
        // Reset car physics
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        else
        {
            rb = GetComponent<Rigidbody>();
            Debug.LogWarning("[CarAgent] Rigidbody was null during episode reset, attempting to get component");
        }
        
        // Reset checkpoint tracking
        nextCheckpointIndex = 1; // Start looking for checkpoint 1 since we're at start/finish line
        timeSinceLastCheckpoint = 0f;
        
        // Reset performance tracking
        totalDistance = 0f;
        lastPosition = transform.position;
        totalCheckpointsPassed = 0;
        totalLapsCompleted = 0;
        episodeReward = 0f;
        
        // Reset lap tracking data
        lastLapTime = 0f;
        // Note: We don't reset currentLap or bestLapTime across episodes
        
        // Reset lap tracking in the LapTrackingSystem
        if (lapTrackingSystem == null)
        {
            lapTrackingSystem = FindFirstObjectByType<LapTrackingSystem>();
            if (lapTrackingSystem != null)
            {
                lapTrackingSystem.RegisterAgent(this);
            }
        }
        
        if (lapTrackingSystem != null)
        {
            lapTrackingSystem.ResetLapData(this);
        }
        
        if (enableDebugLogging)
        {
            Debug.Log($"[CarAgent] Episode started");
        }
        // Add randomization to initial conditions for better generalization
        // Small random rotation to avoid overfitting to exact starting position
        transform.rotation = transform.rotation * Quaternion.Euler(0, Random.Range(-5f, 5f), 0);
        
        // Small random throttle pulse to ensure movement
        carController.Move(0, Random.Range(0.7f, 1.0f), 0);
        
        if (enableDebugLogging)
        {
            Debug.Log($"[CarAgent] Episode started for {gameObject.name}");
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // Extract actions (continuous values)
        float steering = actions.ContinuousActions[0]; // -1 to 1
        // float throttle = actions.ContinuousActions[1]; // 0 to 1
        // float brake = actions.ContinuousActions[2]; // 0 to 1
        float throttle = 0.8f;
        float brake = 0f;
        
        if (Time.frameCount % 60 == 0)
        {
            Debug.Log($"Agent actions - Steering: {steering}, Throttle: {throttle}, Brake: {brake}");
        }
        
        if (forceExploration)
        {
            throttle = explorationThrottle; // Force throttle to move
            brake = 0f;                     // Ensure no braking
        }
        
        // Apply movement through car controller
        carController.Move(steering, throttle, brake);
        
        // Track distance traveled
        float distanceTraveled = Vector3.Distance(transform.position, lastPosition);
        totalDistance += distanceTraveled;
        lastPosition = transform.position;
        
        // Calculate rewards
        CalculateRewards();
        
        // Update timeout timer
        timeSinceLastCheckpoint += Time.fixedDeltaTime;
        if (timeSinceLastCheckpoint > maxTimeWithoutCheckpoint)
        {
            AddReward(-timeoutPenalty);
            episodeReward -= timeoutPenalty;
            EndEpisode();
            
            if (enableDebugLogging)
            {
                Debug.Log($"[CarAgent] Episode ended due to timeout. Total reward: {episodeReward}");
            }
        }
    }
    
    private void CalculateRewards()
{
    // Speed reward (small reward for moving at appropriate speed)
    float speed = rb.linearVelocity.magnitude;
    float forwardSpeed = Vector3.Dot(transform.forward, rb.linearVelocity);
    
    // Only reward if moving forward (penalize backward movement)
    if (forwardSpeed > 0)
    {
        // Progressive reward that scales with speed up to an optimal point
        float optimalSpeed = carController.maxSpeed * 0.7f; // 70% of max speed is optimal
        float speedRatio = Mathf.Clamp01(speed / optimalSpeed);
        
        // Bell curve reward - maximum at optimal speed, less for too slow or too fast
        float speedReward = speedRatio * (2.0f - speedRatio) * speedRewardFactor * Time.fixedDeltaTime;
        AddReward(speedReward);
        episodeReward += speedReward;
    }
    else
    {
        // Stronger penalty for moving backward
        float backwardPenalty = -0.2f * Time.fixedDeltaTime;
        AddReward(backwardPenalty);
        episodeReward += backwardPenalty;
    }
    
    // Direction to next checkpoint for alignment reward
    if (checkpoints != null && checkpoints.Count > 0 && nextCheckpointIndex < checkpoints.Count)
    {
        Transform nextCheckpoint = checkpoints[nextCheckpointIndex];
        Vector3 directionToCheckpoint = (nextCheckpoint.position - transform.position).normalized;
        
        // Alignment reward (reward for facing towards next checkpoint)
        float alignmentWithCheckpoint = Vector3.Dot(transform.forward, directionToCheckpoint);
        
        // Exponential reward for good alignment (strongly rewards direct alignment)
        float alignmentReward = Mathf.Pow(Mathf.Max(0, alignmentWithCheckpoint), 2) * alignmentRewardFactor * Time.fixedDeltaTime;
        
        AddReward(alignmentReward);
        episodeReward += alignmentReward;
        
        // Add progressive reward for getting closer to checkpoint
        float distanceToCheckpoint = Vector3.Distance(transform.position, nextCheckpoint.position);
        float previousDistance = Vector3.Distance(lastPosition, nextCheckpoint.position);
        float distanceReward = (previousDistance - distanceToCheckpoint) * 0.05f;
        
        if (distanceReward > 0)
        {
            AddReward(distanceReward);
            episodeReward += distanceReward;
        }
    }
    
    // Wall proximity penalty using direct raycasts
    CheckWallProximity();
}
    
    private void CheckWallProximity()
    {
        int rayCount = 8; // Number of rays
        float rayLength = 20f;
        
        for (int i = 0; i < rayCount; i++)
        {
            float angle = i * (360f / rayCount);
            Vector3 direction = Quaternion.Euler(0, angle, 0) * transform.forward;
            
            if (Physics.Raycast(transform.position, direction, out RaycastHit hit, rayLength))
            {
                if (hit.collider.CompareTag("Wall") && hit.distance < 0.2f)
                {
                    // Closer hits = higher penalty, weighted by ray direction
                    float[] rayWeights = CalculateRayWeights(rayCount);
                    float weightedPenalty = rayWeights[i] * (1.0f - hit.distance / rayLength) * 0.05f;
                    
                    float proximityPenalty = -weightedPenalty * Time.fixedDeltaTime;
                    AddReward(proximityPenalty);
                    episodeReward += proximityPenalty;
                }
            }
            
            // Draw debug rays
            if (enableDebugLogging)
            {
                Debug.DrawRay(transform.position, direction * rayLength, Color.yellow, 0.1f);
            }
        }
    }
    
    private float[] CalculateRayWeights(int rayCount)
    {
        float[] weights = new float[rayCount];
        
        // Calculate weights based on angle from forward
        for (int i = 0; i < rayCount; i++)
        {
            float angle = (360f / rayCount) * i;
            
            // Front rays (close to 0 or 360 degrees) have higher weight
            float forwardness = Mathf.Cos(angle * Mathf.Deg2Rad);
            
            // Normalize to 0.2-1.0 range (so side/rear rays still matter but less)
            weights[i] = 0.2f + 0.8f * Mathf.Max(0, forwardness);
        }
        
        return weights;
    }
    
    void OnTriggerEnter(Collider other)

    {

        if (other == null)
        {
            Debug.LogWarning("[CarAgent] Collider in OnTriggerEnter is null");
            return;
        }
        // Find lap tracking system if not yet assigned
        if (lapTrackingSystem == null)
        {
            lapTrackingSystem = FindFirstObjectByType<LapTrackingSystem>();
            if (lapTrackingSystem != null)
            {
                lapTrackingSystem.RegisterAgent(this);
            }
        }

        // Make sure checkpoints are initialized
        if (checkpoints == null || checkpoints.Count == 0)
        {
            if (trackGenerator != null)
            {
                checkpoints = trackGenerator.GetCheckpoints();
                Debug.Log($"[CarAgent] Initialized checkpoints in OnTriggerEnter: {(checkpoints != null ? checkpoints.Count : 0)} checkpoints");
            }
        }
        
        string colliderTag = other.tag;
        if (colliderTag == "StartFinish")
        {
            // Handle start/finish line crossing
            if (lapTrackingSystem != null)
            {
                lapTrackingSystem.StartFinishLineCrossed(this);
            }
            else
            {
                // Fallback if no lap tracking system is found
                if (nextCheckpointIndex == 1) // About to start looking for checkpoint 1
                {
                    // Completed a lap
                    totalLapsCompleted++;
                    float lapReward = lapCompletionReward;
                    AddReward(lapReward);
                    episodeReward += lapReward;
                    
                    if (enableDebugLogging)
                    {
                        Debug.Log($"[CarAgent] Lap completed! Total laps: {totalLapsCompleted}");
                    }
                }
            }
            
            // Always count checkpoint 0 (start/finish) as passed
            // Reset timeout and update tracking
            timeSinceLastCheckpoint = 0f;
            totalCheckpointsPassed++;
            
            // Set next checkpoint to 1
            nextCheckpointIndex = 1;
        }
        else if (colliderTag == "Checkpoint")
        {
            // Check if this is the correct next checkpoint
            CheckpointIdentifier identifier = other.GetComponent<CheckpointIdentifier>();
            
            if (identifier != null && identifier.CheckpointIndex == nextCheckpointIndex)
            {
                // Correct checkpoint reached
                float reward = checkpointReward;
                AddReward(reward);
                episodeReward += reward;
                
                // Reset timeout and update tracking
                timeSinceLastCheckpoint = 0f;
                totalCheckpointsPassed++;
                
                // Notify lap tracking system
                if (lapTrackingSystem != null)
                {
                    lapTrackingSystem.CheckpointPassed(this, identifier.CheckpointIndex);
                }
                
                // Update next checkpoint
                nextCheckpointIndex = (nextCheckpointIndex + 1) % checkpoints.Count;
                
                if (enableDebugLogging)
                {
                    Debug.Log($"[CarAgent] Checkpoint {identifier.CheckpointIndex} reached. Next: {nextCheckpointIndex}. Reward: +{reward}");
                }
            }
        }
    }
    
    // Called by the lap tracking system when a lap is completed
    public void OnLapCompleted(int lapNumber, float lapTime)
    {
        currentLap = lapNumber;
        lastLapTime = lapTime;
        
        if (lapTime < bestLapTime)
        {
            bestLapTime = lapTime;
        }
        
        // Apply lap completion reward
        float reward = lapCompletionReward;
        
        // Bonus reward for beating best lap time
        if (lapNumber > 1 && lapTime < bestLapTime + 0.1f)
        {
            reward *= 1.5f;
        }
        
        AddReward(reward);
        episodeReward += reward;
        totalLapsCompleted++;
        
        if (enableDebugLogging)
        {
            Debug.Log($"[CarAgent] Lap {lapNumber} completed in {lapTime:F2}s! Reward: +{reward}");
        }
    }
    
    void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.CompareTag("Wall"))
        {
            // Penalty for wall collision
            float penalty = -wallCollisionPenalty;
            AddReward(penalty);
            episodeReward += penalty;
            
            if (enableDebugLogging)
            {
                Debug.Log($"[CarAgent] Wall collision! Penalty: {penalty}");
            }
        }
    }
    
    public override void CollectObservations(VectorSensor sensor)
    {
        // Check for null references to prevent exceptions
        if (rb == null)
        {
            Debug.LogWarning("Rigidbody is null in CollectObservations");
            rb = GetComponent<Rigidbody>();
            if (rb == null)
            {
                // Add placeholder values if rb is still null
                sensor.AddObservation(0f); // Speed
                sensor.AddObservation(0f); // Forward speed factor
                sensor.AddObservation(0f); // Steering
                sensor.AddObservation(0f); // Throttle
                sensor.AddObservation(0f); // Brake
                sensor.AddObservation(Vector3.zero); // Direction to checkpoint (3 values)
                sensor.AddObservation(0f); // Distance to checkpoint
                sensor.AddObservation(0f); // Angular velocity
                sensor.AddObservation(0f); // Checkpoints passed
                sensor.AddObservation(0f); // Laps completed
                return;
            }
        }
        
        if (carController == null)
        {
            Debug.LogWarning("CarController is null in CollectObservations");
            carController = GetComponent<CarController>();
            if (carController == null)
            {
                // Add default CarController if missing
                carController = gameObject.AddComponent<CarController>();
                carController.maxSpeed = 50f;
                carController.acceleration = 2000f;
                carController.maxSteeringAngle = 30f;
                carController.brakingForce = 500f;
                carController.downforce = 50f;
                
                Debug.Log("Added missing CarController component");
            }
        }
        
        // Check for checkpoints
        if (checkpoints == null || checkpoints.Count == 0)
        {
            if (trackGenerator != null)
            {
                checkpoints = trackGenerator.GetCheckpoints();
                Debug.Log($"Fetched {checkpoints.Count} checkpoints from track generator");
            }
            else
            {
                Debug.LogWarning("No track generator assigned");
            }
        }
        
        // Add basic car information
        sensor.AddObservation(rb.linearVelocity.magnitude / carController.maxSpeed); // Normalized speed
        sensor.AddObservation(Vector3.Dot(transform.forward, rb.linearVelocity.normalized)); // Forward speed factor
        sensor.AddObservation(carController.GetSteeringInput());
        sensor.AddObservation(carController.GetThrottleInput());
        sensor.AddObservation(carController.GetBrakeInput());
        
        // Get next checkpoint direction
        if (checkpoints != null && checkpoints.Count > 0 && nextCheckpointIndex < checkpoints.Count)
        {
            Transform nextCheckpoint = checkpoints[nextCheckpointIndex];
            Vector3 directionToCheckpoint = nextCheckpoint.position - transform.position;
            
            // Add normalized direction to next checkpoint in local space
            Vector3 localDirection = transform.InverseTransformDirection(directionToCheckpoint.normalized);
            sensor.AddObservation(localDirection);
            
            // Add distance to next checkpoint (normalized)
            sensor.AddObservation(directionToCheckpoint.magnitude / 100f); // Normalize with expected max distance
        }
        else
        {
            // If no checkpoints, add zero values
            sensor.AddObservation(Vector3.zero); // Direction (3 values)
            sensor.AddObservation(0f); // Distance
        }
        
        // Add additional car state information
        sensor.AddObservation(rb.angularVelocity.y / 5f); // Normalized rotation speed
        
        // Track progress
        sensor.AddObservation(totalCheckpointsPassed / 10f); // Normalize by expected max checkpoints
        sensor.AddObservation(totalLapsCompleted / 5f); // Normalize by expected max laps
    }    
    
    public override void Heuristic(in ActionBuffers actionsOut)
    {
        // Allow manual control during testing
        var continuousActionsOut = actionsOut.ContinuousActions;
        continuousActionsOut[0] = Input.GetAxis("Horizontal"); // Steering
        continuousActionsOut[1] = Input.GetAxis("Vertical") > 0 ? Input.GetAxis("Vertical") : 0; // Throttle
        continuousActionsOut[2] = Input.GetAxis("Vertical") < 0 ? -Input.GetAxis("Vertical") : 0; // Brake
    }
    
    // Public methods for the genetic algorithm to access
    public int GetCheckpointsPassed() => totalCheckpointsPassed;
    public int GetLapsCompleted() => totalLapsCompleted;
    public float GetTotalDistance() => totalDistance;
    public float GetEpisodeReward() => episodeReward;
    
    // Update to receive new weights from genetic algorithm
    public void UpdateRewardParameters(float speedReward, float checkpointRwd, float lapRwd, float collisionPenalty)
    {
        speedRewardFactor = speedReward;
        checkpointReward = checkpointRwd;
        lapCompletionReward = lapRwd;
        wallCollisionPenalty = collisionPenalty;
    }
}