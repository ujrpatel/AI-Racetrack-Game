using UnityEngine;
using VehicleBehaviour;


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

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        wheelVehicle = GetComponent<WheelVehicle>();

        if (rb == null)
        {
            Debug.LogError("[CarController] Rigidbody component missing!");
        }

        if (wheelVehicle == null)
        {
            Debug.LogError("[CarController] WheelVehicle component missing! Ensure it is enabled if needed.");
        }

        rb.linearDamping = 0.05f;
        rb.angularDamping = 0.05f;
    }

    void FixedUpdate()
    {
        ApplyMovement();
        ApplyDownforce();
    }

    public void Move(float steering, float throttle, float brake)
    {
        steeringInput = Mathf.Clamp(steering, -1f, 1f);
        throttleInput = Mathf.Clamp(throttle, 0f, 1f);
        brakeInput = Mathf.Clamp(brake, 0f, 1f);

        if (wheelVehicle != null)
        {
            wheelVehicle.SetSteering(steeringInput);
            wheelVehicle.SetThrottle(throttleInput);
            wheelVehicle.SetBrake(brakeInput);
        }
    }

    private void ApplyMovement()
    {
        if (wheelVehicle == null)
        {
            // Fallback physics-based movement
            Vector3 force = transform.forward * throttleInput * acceleration;
            rb.AddForce(force, ForceMode.Force);

            if (brakeInput > 0)
            {
                rb.AddForce(-rb.linearVelocity.normalized * brakeInput * brakingForce, ForceMode.Force);
            }

            float turn = steeringInput * maxSteeringAngle * Time.fixedDeltaTime;
            rb.AddTorque(Vector3.up * turn, ForceMode.Force);
        }
    }

    private void ApplyDownforce()
    {
        rb.AddForce(-transform.up * downforce * rb.linearVelocity.magnitude);
    }

    public float GetSteeringInput() => steeringInput;
    public float GetThrottleInput() => throttleInput;
    public float GetBrakeInput() => brakeInput;
}
