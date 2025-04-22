using UnityEngine;
using VehicleBehaviour;

public class CarController : MonoBehaviour
{
    [Header("Vehicle")]
    public WheelVehicle wheelVehicle;

    [Header("Track Reference")]
    [SerializeField] private SplineTrackGenerator trackGenerator;

    [Header("Control Settings")]
    public float maxSteer = 1f;
    public float maxThrottle = 1f;
    public float maxBrake = 1f;

    [Header("Debug Metrics")]
    public float CurrentSteer { get; private set; }
    public float CurrentThrottle { get; private set; }
    public float CurrentBrake { get; private set; }

    void Awake()
    {
        // Get WheelVehicle reference if not assigned
        if (wheelVehicle == null)
        {
            wheelVehicle = GetComponent<WheelVehicle>();
            if (wheelVehicle == null)
            {
                Debug.LogError("WheelVehicle component not found on car prefab!");
            }
        }


        // Find SplineTrackGenerator if not assigned
        if (trackGenerator == null)
        {
            trackGenerator = FindFirstObjectByType<SplineTrackGenerator>();
            if (trackGenerator == null)
            {
                Debug.LogError("[CarController] SplineTrackGenerator not found in scene!");
            }
        }
    }

    public void ApplyControl(float steer, float throttle, float brake)
    {
        if (wheelVehicle == null) return;

        // Clamp and store inputs for debugging
        CurrentSteer = Mathf.Clamp(steer, -1f, 1f);
        CurrentThrottle = Mathf.Clamp(throttle, -1f, 1f); // Allow negative for reverse
        CurrentBrake = Mathf.Clamp(brake, 0f, 1f);

        // Apply to vehicle physics
        wheelVehicle.Steering = CurrentSteer * maxSteer;
        wheelVehicle.Throttle = CurrentThrottle * maxThrottle;
        wheelVehicle.SetBrake(CurrentBrake * maxBrake);
        Debug.Log($"[ApplyControl] Steer: {steer}, Throttle: {throttle}, Brake: {brake}");

    }

    public void ResetCar()
    {
        if (wheelVehicle == null)
        {
            Debug.LogError("WheelVehicle is not assigned in CarController!", gameObject);
            return;
        }

        // Reset control inputs
        wheelVehicle.SetSteering(0f);
        wheelVehicle.SetThrottle(0f);
        wheelVehicle.SetBrake(0f);

        // Position at first checkpoint if available
        if (trackGenerator != null)
        {
            var checkpoints = trackGenerator.GetCheckpoints();
            if (checkpoints != null && checkpoints.Count > 0)
            {
                Transform startCheckpoint = checkpoints[0];
                Vector3 spawnPosition = startCheckpoint.position - (startCheckpoint.forward * 5f) + (Vector3.up * 0.5f);
                Quaternion spawnRotation = startCheckpoint.rotation;

                // Update spawn position (using reflection to access private fields)
                System.Reflection.FieldInfo posField = wheelVehicle.GetType().GetField("spawnPosition", 
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                
                System.Reflection.FieldInfo rotField = wheelVehicle.GetType().GetField("spawnRotation", 
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                
                if (posField != null && rotField != null)
                {
                    posField.SetValue(wheelVehicle, spawnPosition);
                    rotField.SetValue(wheelVehicle, spawnRotation);
                    
                    // Reset vehicle to the spawn position
                    wheelVehicle.ResetPos();
                }
                else
                {
                    // Direct method if reflection fails
                    transform.position = spawnPosition;
                    transform.rotation = spawnRotation;
                    
                    Rigidbody rb = GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.linearVelocity = Vector3.zero;
                        rb.angularVelocity = Vector3.zero;
                    }
                }
            }
            else
            {
                Debug.LogWarning("[CarController] No checkpoints found! Using default WheelVehicle spawn position.");
                wheelVehicle.ResetPos();
            }
        }
        else
        {
            Debug.LogWarning("[CarController] SplineTrackGenerator not assigned, using default WheelVehicle spawn position.");
            wheelVehicle.ResetPos();
        }
    }
}