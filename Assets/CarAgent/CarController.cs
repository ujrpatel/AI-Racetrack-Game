using UnityEngine;
using VehicleBehaviour;
using System.Collections;

public class CarController : MonoBehaviour
{
    private Rigidbody rb;
    private WheelVehicle wheelVehicle;

    public float maxSpeed = 50f;
    public float acceleration = 2000f;
    public float maxSteeringAngle = 30f;
    public float brakingForce = 500f;
    public float downforce = 50f;
    public float traction = 1f;

    private float throttleInput = 0f;
    private float steeringInput = 0f;
    private float brakeInput = 0f;
    
    public bool debugMode = false;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        wheelVehicle = GetComponent<WheelVehicle>();

        if (rb == null)
        {
            Debug.LogError("[CarController] Rigidbody component missing!");
            rb = gameObject.AddComponent<Rigidbody>();
            ConfigureRigidbody(rb);
        }
        else
        {
            ConfigureRigidbody(rb);
        }

        // Check for WheelVehicle component but don't require it
        if (wheelVehicle == null)
        {
            Debug.Log("[CarController] Using built-in physics system for movement.");
        }
        else
        {
            Debug.Log("[CarController] WheelVehicle component found, will use it if active.");
            
            // Try to optimize WheelVehicle if present
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
                Debug.LogWarning($"Failed to configure WheelVehicle: {e.Message}");
            }
        }

        StartCoroutine(InitialThrottlePulse());
    }
    
    private void ConfigureRigidbody(Rigidbody rb)
    {
        // Set reasonable physical properties for a car
        rb.mass = 1000f;
        rb.linearDamping = 0.05f;
        rb.angularDamping = 0.7f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        
        // Ensure center of mass is low for better stability
        rb.centerOfMass = new Vector3(0, -0.5f, 0);
    }

    IEnumerator InitialThrottlePulse()
    {
        yield return new WaitForSeconds(0.5f);
        // Apply a strong initial throttle to overcome static friction
        for (int i = 0; i < 30; i++)
        {
            Move(0, 1.0f, 0);
            yield return new WaitForFixedUpdate();
        }
    }

    void FixedUpdate()
    {
        ApplyMovement();
        ApplyDownforce();
        
        if (debugMode && Time.frameCount % 60 == 0)
        {
            Debug.Log($"Speed: {GetSpeed():F2} m/s ({GetSpeed() * 3.6f:F2} km/h), " + 
                  $"Throttle: {throttleInput:F2}, Steering: {steeringInput:F2}");
        }
    }

    public void Move(float steering, float throttle, float brake)
    {
        steeringInput = Mathf.Clamp(steering, -1f, 1f);
        throttleInput = Mathf.Clamp(throttle, 0f, 1f);
        brakeInput = Mathf.Clamp(brake, 0f, 1f);
        
        // Use WheelVehicle if available and active, otherwise use direct physics
        if (wheelVehicle != null)
        {
            wheelVehicle.SetSteering(steeringInput);
            wheelVehicle.SetThrottle(throttleInput);
            wheelVehicle.SetBrake(brakeInput);
        }
    }

    private void ApplyMovement()
{
    if (rb == null) return;
    
    // Calculate forward force based on throttle
    Vector3 force = transform.forward * throttleInput * acceleration * 2.0f;
    rb.AddForce(force, ForceMode.Force);
    
    // Apply braking
    if (brakeInput > 0)
    {
        Vector3 brakeForce = -rb.linearVelocity.normalized * brakeInput * brakingForce;
        rb.AddForce(brakeForce, ForceMode.Force);
    }
    
    // Reduce drag to maintain speed better
    rb.linearDamping = 0.01f;
    
    // Improved steering logic - adjust based on speed
    float speedFactor = Mathf.Clamp01(rb.linearVelocity.magnitude / maxSpeed);
    
    // More steering at low speeds, less at high speeds for stability
    float steeringMultiplier = 1.0f - (speedFactor * 0.5f);  
    
    // Apply steering as torque
    float steeringTorque = steeringInput * maxSteeringAngle * steeringMultiplier * 20f;
    rb.AddTorque(Vector3.up * steeringTorque, ForceMode.Force);
    
    // Add some direct rotation for more responsive steering at low speeds
    if (speedFactor < 0.5f && Mathf.Abs(steeringInput) > 0.1f)
    {
        float directTurn = steeringInput * steeringMultiplier * Time.deltaTime * 50.0f;
        transform.Rotate(0, directTurn, 0);
    }
    
    // Log speed periodically for debugging
    if (Time.frameCount % 120 == 0)
    {
        Debug.Log($"Current Speed: {rb.linearVelocity.magnitude} m/s, Force: {force.magnitude}, Steering: {steeringTorque}");
    }
}   

    private void ApplyDownforce()
    {
        if (rb == null) return;
        
        // More downforce at higher speeds
        float speed = rb.linearVelocity.magnitude;
        float dynamicDownforce = downforce * Mathf.Clamp01(speed / 10f);
        rb.AddForce(-transform.up * dynamicDownforce, ForceMode.Force);
    }

    public float GetSteeringInput() => steeringInput;
    public float GetThrottleInput() => throttleInput;
    public float GetBrakeInput() => brakeInput;
    public float GetSpeed() => rb != null ? rb.linearVelocity.magnitude : 0f;
    public Vector3 GetVelocity() => rb != null ? rb.linearVelocity : Vector3.zero;
    public float GetForwardSpeed() => rb != null ? Vector3.Dot(transform.forward, rb.linearVelocity) : 0f;
}