using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

public class CarAgent : Agent
{
    private CarController carController;
    private Rigidbody rb;

    void Start()
    {
        carController = GetComponent<CarController>();
        rb = GetComponent<Rigidbody>();

        if (carController == null)
        {
            Debug.LogError("[CarAgent] CarController component missing!");
        }

        if (rb == null)
        {
            Debug.LogError("[CarAgent] Rigidbody component missing!");
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float steering = actions.ContinuousActions[0]; // -1 to 1
        float throttle = actions.ContinuousActions[1]; // 0 to 1
        float brake = actions.ContinuousActions[2]; // 0 to 1

        Debug.Log($"[CarAgent] Throttle Input: {throttle}");
        Debug.Log($"[CarAgent] Brake Input: {brake}");


        // Debugging: Print input values
        Debug.Log($"[CarAgent] Steering: {steering}, Throttle: {throttle}, Brake: {brake}");

        // Apply movement
        carController.Move(steering, throttle, brake);

        // Reward for moving forward
        float speed = rb.linearVelocity.magnitude;
        AddReward(speed * 0.01f); // Small reward for moving faster

        // Penalize for going backward
        if (Vector3.Dot(transform.forward, rb.linearVelocity) < 0)
        {
            AddReward(-0.1f);
        }


        if (Physics.Raycast(transform.position, transform.forward, out RaycastHit hit, 2f))
        {
            if (hit.collider.CompareTag("Wall")) // Make sure walls have this tag
            {
                AddReward(-1.0f); // Big penalty for crashing
                EndEpisode(); // Restart the episode
            }
        }
        // Debugging: Print car speed
        if (rb != null)
        {
            Debug.Log($"[CarAgent] Car Speed: {rb.linearVelocity.magnitude}");
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        sensor.AddObservation(transform.position);
        sensor.AddObservation(transform.forward);
        sensor.AddObservation(rb.linearVelocity.magnitude);
        sensor.AddObservation(carController.GetSteeringInput());
        sensor.AddObservation(carController.GetThrottleInput());
        sensor.AddObservation(carController.GetBrakeInput());

        // Debugging: Print observation values
        Debug.Log($"[CarAgent] Position: {transform.position}, Forward: {transform.forward}, Speed: {rb.linearVelocity.magnitude}");
    }
}
