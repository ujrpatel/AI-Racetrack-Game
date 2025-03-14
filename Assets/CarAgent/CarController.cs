// File: carcontroller.cs
// Complete replacement with improved implementation

using UnityEngine;
using VehicleBehaviour;
using System.Collections;

public class CarController : MonoBehaviour
{
    private Rigidbody rb;
    private WheelVehicle wheelVehicle;

    public float maxSpeed = 50f;
    public float acceleration = 1500f;
    public float maxSteeringAngle = 30f;
    public float brakingForce = 500f;
    public float downforce = 50f;

    private float throttleInput = 0f;
    private float steeringInput = 0f;
    private float brakeInput = 0f;
    
    // Add a flag to track which movement system to use
    public bool useWheelVehicle = true;
    
    // Debug flags
    public bool showDebugLogs = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        wheelVehicle = GetComponent<WheelVehicle>();

        if (rb == null)
        {
            Debug.LogError("[CarController] Rigidbody component missing!");
            rb = gameObject.AddComponent<Rigidbody>();
        }

        if (wheelVehicle == null)
        {
            Debug.Log("[CarController] WheelVehicle component not found, using simplified physics.");
            useWheelVehicle = false;
        }
        else
        {
            Debug.Log("WheelVehicle component found");
            
            // Try to optimize the WheelVehicle settings if we're using it
            try {
                var diffGearingField = wheelVehicle.GetType().GetField("diffGearing", 
                    System.Reflection.BindingFlags.Instance | 
                    System.Reflection.BindingFlags.Public | 
                    System.Reflection.BindingFlags.NonPublic);
                
                if (diffGearingField != null)
                {
                    diffGearingField.SetValue(wheelVehicle, 4.0f);
                }
                
                var diffGearingProperty = wheelVehicle.GetType().GetProperty("DiffGearing");
                if (diffGearingProperty != null)
                {
                    diffGearingProperty.SetValue(wheelVehicle, 4.0f);
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Error configuring WheelVehicle: {e.Message}");
            }
        }

        // Configure rigidbody for better physics behavior
        if (rb != null)
        {
            rb.mass = 1000;
            rb.linearDamping = 0.1f;
            rb.angularDamping = 0.5f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        }
        
        // Apply initial movement to overcome static friction
        StartCoroutine(InitialThrottlePulse());
    }
    
    IEnumerator InitialThrottlePulse()
    {
        yield return new WaitForSeconds(0.5f);
        // Apply a short throttle burst
        for (int i = 0; i < 30; i++)
        {
            Move(0, 1, 0);
            yield return new WaitForFixedUpdate();
        }
    }
    
    void FixedUpdate()
    {
        if (showDebugLogs && Time.frameCount % 60 == 0)
        {
            Debug.Log($"Speed: {rb.linearVelocity.magnitude} m/s, " + 
                      $"Throttle: {throttleInput}, " + 
                      $"Using WheelVehicle: {useWheelVehicle}");
        }
        
        ApplyMovement();
        ApplyDownforce();
    }

    public void Move(float steering, float throttle, float brake)
    {
        steeringInput = Mathf.Clamp(steering, -1f, 1f);
        throttleInput = Mathf.Clamp(throttle, 0f, 1f);
        brakeInput = Mathf.Clamp(brake, 0f, 1f);
        
        if (showDebugLogs && Time.frameCount % 300 == 0)
        {
            Debug.Log($"CarController.Move - Steering: {steeringInput}, Throttle: {throttleInput}, Brake: {brakeInput}");
        }
        
        if (useWheelVehicle && wheelVehicle != null && wheelVehicle.isActiveAndEnabled)
        {
            wheelVehicle.SetSteering(steeringInput);
            wheelVehicle.SetThrottle(throttleInput);
            wheelVehicle.SetBrake(brakeInput);
        }
    }

    private void ApplyMovement()
    {
        if (!useWheelVehicle || wheelVehicle == null || !wheelVehicle.isActiveAndEnabled)
        {
            // Direct physics-based movement when WheelVehicle is unavailable
            ApplyMovementDirectly();
        }
    }
    
    private void ApplyMovementDirectly()
    {
        if (rb == null) return;
        
        // Calculate forward force based on throttle
        Vector3 force = transform.forward * throttleInput * acceleration;
        rb.AddForce(force, ForceMode.Force);
        
        // Apply braking
        if (brakeInput > 0)
        {
            Vector3 brakeForce = -rb.linearVelocity.normalized * brakeInput * brakingForce;
            rb.AddForce(brakeForce, ForceMode.Force);
        }
        
        // Apply steering torque (stronger at lower speeds for better control)
        float speedFactor = Mathf.Clamp01(1.0f - (rb.linearVelocity.magnitude / maxSpeed));
        float steeringPower = steeringInput * maxSteeringAngle * (1.0f + speedFactor * 2.0f);
        rb.AddTorque(Vector3.up * steeringPower, ForceMode.Force);
        
        // Add stability - dampen unwanted sideways velocity
        Vector3 localVelocity = transform.InverseTransformDirection(rb.linearVelocity);
        float sidewaysVelocity = localVelocity.x;
        
        // Apply a counter force to reduce sideways sliding (stronger at higher speeds)
        Vector3 stabilizingForce = -transform.right * sidewaysVelocity * 0.5f * rb.mass;
        rb.AddForce(stabilizingForce, ForceMode.Force);
        
        // Limit top speed
        if (rb.linearVelocity.magnitude > maxSpeed)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
        }
    }

    private void ApplyDownforce()
    {
        if (rb == null) return;
        
        // More downforce at higher speeds for better stability
        float speedFactor = rb.linearVelocity.magnitude / maxSpeed;
        float dynamicDownforce = downforce * speedFactor;
        rb.AddForce(-transform.up * dynamicDownforce, ForceMode.Force);
    }

    public float GetSteeringInput() => steeringInput;
    public float GetThrottleInput() => throttleInput;
    public float GetBrakeInput() => brakeInput;
    public float GetSpeed() => rb != null ? rb.linearVelocity.magnitude : 0f;
}