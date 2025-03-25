using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Collections;
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

    public float stationaryTimeout = 3.0f;
    private float timeSpentStationary = 0f;
    private bool hasCompletedLap = false;
    private float episodeStartTime;
    private float minSpeedThreshold = 2.0f; // Consider stationary below this speed
    
    // Track progress monitoring
    private float progressTrackingInterval = 5.0f; // Check progress every 5 seconds
    private float lastProgressCheckTime = 0f;
    private float lastCheckpointProgress = 0f;
    private Vector3 lastProgressPosition;
    private bool isCircling = false;
    private float circlingPenalty = 0.2f;
    
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
    
    // Flag to prevent multiple episode endings
    private bool isEndingEpisode = false;
    
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
        
        // Find lap tracking system if available
        lapTrackingSystem = FindFirstObjectByType<LapTrackingSystem>();
        if (lapTrackingSystem != null)
        {
            lapTrackingSystem.RegisterAgent(this);
        }
        
        // Cache checkpoints if track generator is available
        if (trackGenerator != null)
{
    checkpoints = trackGenerator.GetCheckpoints();
    if (checkpoints != null && checkpoints.Count > 0)
    {
        Debug.Log($"[CarAgent] {gameObject.name}: Found {checkpoints.Count} checkpoints from track generator");
    }
    else
    {
        Debug.LogWarning($"[CarAgent] {gameObject.name}: No checkpoints found from track generator!");
    }
}
else
{
    Debug.LogWarning($"[CarAgent] {gameObject.name}: No track generator assigned!");
}
        
        // Initialize progress tracking
        lastProgressPosition = transform.position;
        lastProgressCheckTime = Time.time;
    }
    
    void Update()
    {
        // Track progress and check for circling behavior
        if (Time.time - lastProgressCheckTime > progressTrackingInterval)
        {
            CheckProgressAlongTrack();
            lastProgressCheckTime = Time.time;
        }
    }
    
    private void CheckProgressAlongTrack()
    {
        // Calculate how much progress we've made toward the next checkpoint
        if (checkpoints != null && checkpoints.Count > 0 && nextCheckpointIndex < checkpoints.Count)
        {
            Transform nextCheckpoint = checkpoints[nextCheckpointIndex];
            
            // Calculate progress as distance reduction to next checkpoint
            float currentDistance = Vector3.Distance(transform.position, nextCheckpoint.position);
            float previousDistance = Vector3.Distance(lastProgressPosition, nextCheckpoint.position);
            float progressMade = previousDistance - currentDistance;
            
            // Check if we're not making significant progress or driving in circles
            float distanceTraveled = Vector3.Distance(transform.position, lastProgressPosition);
            float progressEfficiency = Mathf.Abs(progressMade) / (distanceTraveled + 0.01f);
            
            if (distanceTraveled > 10f && progressEfficiency < 0.1f)
            {
                // Car is moving but not making progress toward next checkpoint
                isCircling = true;
                
                // Apply circling penalty
                AddReward(-circlingPenalty);
                episodeReward -= circlingPenalty;
                
                if (enableDebugLogging)
                {
                    Debug.Log($"[CarAgent] Detected circling behavior. Efficiency: {progressEfficiency:F2}");
                }
            }
            else
            {
                isCircling = false;
            }
            
            // Update for next check
            lastProgressPosition = transform.position;
            lastCheckpointProgress = currentDistance;
        }
    }
    
    private void ResetPosition()
    {
        if (trackGenerator == null || checkpoints == null || checkpoints.Count == 0)
        {
            // Try to get checkpoints if not already set
            if (trackGenerator != null)
            {
                checkpoints = trackGenerator.GetCheckpoints();
            }
            
            if (checkpoints == null || checkpoints.Count == 0)
            {
                Debug.LogWarning("[CarAgent] No checkpoints available for reset position");
                return;
            }
        }
        
        // Use start/finish line for reset
        transform.position = checkpoints[0].position + Vector3.up * 0.5f; // Slight lift to avoid ground collision
        transform.rotation = checkpoints[0].rotation;
    }
    
    public override void OnEpisodeBegin()
    {
        // Reset position to start or last checkpoint
        ResetPosition();
        
        // Reset physics
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        
        // Reset tracking variables
        nextCheckpointIndex = 1;
        timeSinceLastCheckpoint = 0f;
        totalDistance = 0f;
        lastPosition = transform.position;
        lastProgressPosition = transform.position;
        totalCheckpointsPassed = 0;
        totalLapsCompleted = 0;
        episodeReward = 0f;
        hasCompletedLap = false;
        timeSpentStationary = 0f;
        episodeStartTime = Time.time;
        lastProgressCheckTime = Time.time;
        isCircling = false;
        isEndingEpisode = false;
        
        // Apply an initial throttle pulse to overcome static friction
        if (carController != null)
        {
            carController.Move(0, 0.8f, 0);
        }
        
        // Register with lap tracking system if available
        if (lapTrackingSystem == null)
        {
            lapTrackingSystem = FindFirstObjectByType<LapTrackingSystem>();
            if (lapTrackingSystem != null)
            {
                lapTrackingSystem.RegisterAgent(this);
                lapTrackingSystem.ResetLapData(this);
            }
        }
        else
        {
            lapTrackingSystem.ResetLapData(this);
        }
        
        if (enableDebugLogging)
        {
            Debug.Log($"[CarAgent] Episode began for {gameObject.name}");
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        // Extract actions - continuous values
        float steering = actions.ContinuousActions[0]; // -1 to 1
        float throttle = actions.ContinuousActions[1]; // 0 to 1 
        float brake = actions.ContinuousActions[2]; // 0 to 1
        
        if (forceExploration)
        {
            throttle = explorationThrottle;
            brake = 0f;
        }
        
        // Apply movement through car controller
        carController.Move(steering, throttle, brake);
        
        // Track distance traveled
        float distanceTraveled = Vector3.Distance(transform.position, lastPosition);
        totalDistance += distanceTraveled;
        lastPosition = transform.position;
        
        // Calculate rewards
        CalculateRewards();
        
        // Check for stationary car
        CheckIfStationary();
        
        // Update timeout timer
        timeSinceLastCheckpoint += Time.fixedDeltaTime;
        if (timeSinceLastCheckpoint > maxTimeWithoutCheckpoint)
        {
            AddReward(-timeoutPenalty);
            episodeReward -= timeoutPenalty;
            
            if (enableDebugLogging)
            {
                Debug.Log($"[CarAgent] Episode ended due to checkpoint timeout. Total reward: {episodeReward}");
            }
            
            StartCoroutine(SafeEndEpisode());
        }
    }
    
    private void CheckIfStationary()
    {
        float speed = rb.linearVelocity.magnitude;
        
        if (speed < minSpeedThreshold)
        {
            timeSpentStationary += Time.fixedDeltaTime;
            
            if (timeSpentStationary > stationaryTimeout)
            {
                // Penalize and end episode if car is stuck
                AddReward(-timeoutPenalty * 0.5f);
                episodeReward -= timeoutPenalty * 0.5f;
                
                if (enableDebugLogging)
                {
                    Debug.Log($"[CarAgent] Episode ended because car was stationary for too long. Total reward: {episodeReward}");
                }
                
                StartCoroutine(SafeEndEpisode());
            }
        }
        else
        {
            // Reset stationary timer if car is moving
            timeSpentStationary = 0f;
        }
    }

    private void CalculateRewards()
    {
        // Speed reward (encourage forward movement)
        float speed = rb.linearVelocity.magnitude;
        float forwardSpeed = Vector3.Dot(transform.forward, rb.linearVelocity);
        
        // Only reward if moving forward (penalize backward movement)
        if (forwardSpeed > 0)
        {
            // Progressive reward that increases with speed up to 70% of max speed
            float optimalSpeed = carController.maxSpeed * 0.7f;
            float speedRatio = Mathf.Clamp01(speed / optimalSpeed);
            
            // Bell curve reward: maximum at optimal speed, less for too slow or too fast
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
            
            // Exponential reward for good alignment
            if (alignmentWithCheckpoint > 0)
            {
                float alignmentReward = Mathf.Pow(alignmentWithCheckpoint, 2) * alignmentRewardFactor * Time.fixedDeltaTime;
                AddReward(alignmentReward);
                episodeReward += alignmentReward;
            }
            
            // Distance progress reward - reward for getting closer to next checkpoint
            float currentDistanceToCheckpoint = Vector3.Distance(transform.position, nextCheckpoint.position);
            float previousDistanceToCheckpoint = Vector3.Distance(lastPosition, nextCheckpoint.position);
            float distanceReduction = previousDistanceToCheckpoint - currentDistanceToCheckpoint;
            
            if (distanceReduction > 0)
            {
                float progressReward = distanceReduction * 0.1f; // Small reward for progress
                AddReward(progressReward);
                episodeReward += progressReward;
            }
        }
        
        // Wall proximity penalties
        CheckWallProximity();
        
        // Penalty for circling behavior
        if (isCircling)
        {
            float penalty = -circlingPenalty * Time.fixedDeltaTime;
            AddReward(penalty);
            episodeReward += penalty;
        }
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
                if (hit.collider.CompareTag("Wall") && hit.distance < 0.5f)
                {
                    // Closer hits = higher penalty, weighted by ray direction
                    float[] rayWeights = CalculateRayWeights(rayCount);
                    float weightedPenalty = rayWeights[i] * (1.0f - hit.distance / rayLength) * 0.05f;
                    
                    float proximityPenalty = -weightedPenalty * Time.fixedDeltaTime;
                    AddReward(proximityPenalty);
                    episodeReward += proximityPenalty;
                    
                    // If extremely close to wall, end episode
                    if (hit.distance < 0.2f)
                    {
                        float penalty = -wallCollisionPenalty * 0.5f;
                        AddReward(penalty);
                        episodeReward += penalty;
                        
                        if (enableDebugLogging)
                        {
                            Debug.Log($"[CarAgent] Too close to wall! Penalty: {penalty}");
                        }
                        
                        StartCoroutine(SafeEndEpisode());
                        break;
                    }
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
    
    public void SetNextCheckpointIndex(int index)
    {
        nextCheckpointIndex = index;
    }
    
    void OnTriggerEnter(Collider other)
    {
        // Safety check
        if (other == null) return;
        
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
            }
        }
        
        if (other.CompareTag("StartFinish"))
        {
            // Handle start/finish line crossing
            if (lapTrackingSystem != null)
            {
                lapTrackingSystem.StartFinishLineCrossed(this);
            }
            else
            {
                // Fallback if no lap tracking system
                HandleStartFinishLineCrossing();
            }
            
            // Reset timeout and update tracking
            timeSinceLastCheckpoint = 0f;
            totalCheckpointsPassed++;
            
            // Set next checkpoint to 1
            nextCheckpointIndex = 1;
        }
        else if (other.CompareTag("Checkpoint"))
        {
            // Check if this is the correct next checkpoint
            CheckpointIdentifier identifier = other.GetComponent<CheckpointIdentifier>();
            
            if (identifier != null)
            {
                // Notify lap tracking system
                if (lapTrackingSystem != null)
                {
                    lapTrackingSystem.CheckpointPassed(this, identifier.CheckpointIndex);
                }
                else
                {
                    // Fallback if no lap tracking system
                    HandleCheckpointPassing(identifier);
                }
            }
        }
    }
    
    // Fallback methods for when LapTrackingSystem is not available
    private void HandleCheckpointPassing(CheckpointIdentifier identifier)
    {
        if (identifier.CheckpointIndex == nextCheckpointIndex)
        {
            // Correct checkpoint reached
            float reward = checkpointReward;
            AddReward(reward);
            episodeReward += reward;
            
            // Reset timeout and update tracking
            timeSinceLastCheckpoint = 0f;
            totalCheckpointsPassed++;
            
            // Update next checkpoint
            nextCheckpointIndex = (nextCheckpointIndex + 1) % checkpoints.Count;
            
            if (enableDebugLogging)
            {
                Debug.Log($"[CarAgent] Checkpoint {identifier.CheckpointIndex} reached. Next: {nextCheckpointIndex}. Reward: +{reward}");
            }
        }
    }
    
    private void HandleStartFinishLineCrossing()
    {
        // Check if this is a lap completion (which means we've passed most checkpoints)
        if (totalCheckpointsPassed >= checkpoints.Count * 0.8f) // 80% of checkpoints is enough
        {
            // Completed a lap
            totalLapsCompleted++;
            hasCompletedLap = true;
            
            float lapTime = Time.time - episodeStartTime;
            
            // Stronger reward for completing lap
            float lapReward = lapCompletionReward;
            AddReward(lapReward);
            episodeReward += lapReward;
            
            // Bonus reward for completing lap quickly
            if (lapTime < 120f) // Adjust based on your track
            {
                float timeBonus = Mathf.Clamp(120f - lapTime, 0f, 60f) * 0.1f;
                AddReward(timeBonus);
                episodeReward += timeBonus;
            }
            
            if (enableDebugLogging)
            {
                Debug.Log($"[CarAgent] Lap completed in {lapTime:F2}s! Total laps: {totalLapsCompleted}");
            }
            
            // Reset lap start time
            episodeStartTime = Time.time;
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
        hasCompletedLap = true;
        
        if (enableDebugLogging)
        {
            Debug.Log($"[CarAgent] Lap {lapNumber} completed in {lapTime:F2}s! Reward: +{reward}");
        }
    }
    
    void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Wall"))
        {
            // Stronger penalty for wall collision
            float penalty = -wallCollisionPenalty;
            AddReward(penalty);
            episodeReward += penalty;
            
            if (enableDebugLogging)
            {
                Debug.Log($"[CarAgent] Wall collision! Penalty: {penalty}, Episode terminated.");
            }
            
            // Start coroutine to safely end episode
            StartCoroutine(SafeEndEpisode());
        }
    }

    private IEnumerator SafeEndEpisode()
    {
        // Prevent multiple calls
        if (isEndingEpisode) yield break;
        isEndingEpisode = true;
        
        // Wait for end of frame to avoid calling EndEpisode during physics step
        yield return new WaitForEndOfFrame();
        
        // Call ML-Agents EndEpisode before deactivating
        EndEpisode();
        
        // Deactivate the agent GameObject
        gameObject.SetActive(false);
        
        // Then notify the genetic manager that this episode is complete
        GeneticPPOManager manager = FindFirstObjectByType<GeneticPPOManager>();
        if (manager != null)
        {
            manager.NotifyGenomeComplete(this);
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
                sensor.AddObservation(0f); // Is circling
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
        
        // Add circling detection
        sensor.AddObservation(isCircling ? 1f : 0f);
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
    
    // Improved fitness calculation that prioritizes checkpoint progress
    public float CalculateFitness()
    {
        // Base fitness on checkpoints passed (highest priority)
        float checkpointScore = totalCheckpointsPassed * 10f;
        
        // Add bonus for completed laps
        float lapScore = totalLapsCompleted * 100f;
        
        // Add distance component but with less weight if not making checkpoint progress
        // This prevents reward for driving in circles
        float progressRatio = Mathf.Clamp01((float)totalCheckpointsPassed / (checkpoints != null ? checkpoints.Count * 2f : 10f));
        float distanceScore = totalDistance * 0.1f * progressRatio;
        
        // Add speed component only if completed lap or making good progress
        float speedScore = 0f;
        if (hasCompletedLap || progressRatio > 0.5f)
        {
            speedScore = carController.GetSpeed() * 5f * progressRatio;
        }
        
        // Add time efficiency component
        float timeBonus = 0f;
        if (totalLapsCompleted > 0)
        {
            float avgLapTime = (Time.time - episodeStartTime) / totalLapsCompleted;
            timeBonus = Mathf.Max(0, 300f - avgLapTime) * 0.2f;
        }
        
        // Combine scores
        return checkpointScore + lapScore + distanceScore + speedScore + timeBonus;
    }
}