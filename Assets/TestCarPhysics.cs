using UnityEngine;

public class TestCarPhysics : MonoBehaviour
{
    public GameObject carPrefab; // Assign the same prefab your algorithm uses
    private GameObject carInstance;
    private CarController carController;
    private Transform spawnPoint;
    public SplineTrackGenerator trackGenerator; 
    private LapTrackingSystem lapTrackingSystem;    
    void Start()
    {
        spawnPoint = transform;
        // Find or create a spawn point
        lapTrackingSystem = FindFirstObjectByType<LapTrackingSystem>();
        if (lapTrackingSystem == null)
        {
            Debug.LogWarning("No LapTrackingSystem found. Creating one...");
            GameObject lapSystemObj = new GameObject("LapTrackingSystem");
            lapTrackingSystem = lapSystemObj.AddComponent<LapTrackingSystem>();
            
            // Connect to track generator
            if (trackGenerator != null)
                lapTrackingSystem.trackGenerator = trackGenerator;
        }
        
        // Spawn the car
        SpawnTestCar();
    }
    
    void SpawnTestCar()
    {
        Vector3 spawnPosition = spawnPoint.position;
    Quaternion spawnRotation = spawnPoint.rotation;
    
    // Get the first checkpoint position from track generator
    if (trackGenerator != null)
    {
        var checkpoints = trackGenerator.GetCheckpoints();
        if (checkpoints != null && checkpoints.Count > 0)
        {
            // Get the first checkpoint (usually the start/finish line)
            Transform startCheckpoint = checkpoints[0];
            
            // Position the car slightly before the checkpoint
            // Use the checkpoint's forward direction to move back a bit
            spawnPosition = startCheckpoint.position - (startCheckpoint.forward * 5f) + (Vector3.up * 0.5f);
            spawnRotation = startCheckpoint.rotation;
            
            Debug.Log($"Spawning car at checkpoint position: {spawnPosition}");
        }
        else
        {
            Debug.LogWarning("No checkpoints found in the track generator");
        }
    }
    else
    {
        Debug.LogWarning("No track generator assigned");
    }
    
    // Instantiate your car prefab at the determined position
    carInstance = Instantiate(carPrefab, spawnPosition, spawnRotation);
    
    // Get the car controller
    carController = carInstance.GetComponent<CarController>();
    if (carController == null)
    {
        Debug.LogError("CarController missing from prefab!");
        return;
    }
    
    // Tag the car as "Player" for checkpoint detection
    carInstance.tag = "Player";
    
    TestCarLapTracker lapTracker = carInstance.AddComponent<TestCarLapTracker>();
    lapTracker.trackingSystem = lapTrackingSystem;
    // Disable any AI components on the car
    DisableAIComponents(carInstance);
    
    // Set up camera to follow this car
    SetupCamera();
    // Register with lap tracking system
    if (lapTrackingSystem != null)
    {
        // Get the CarAgent component that might exist on the prefab
        CarAgent agent = carInstance.GetComponent<CarAgent>();
        
        // Register with the lap system (even if agent is disabled)
        if (agent != null)
        {
            lapTrackingSystem.RegisterAgent(agent);
            lapTrackingSystem.ResetLapData(agent);
        }
    }

    }
    
    void DisableAIComponents(GameObject car)
    {
        // Disable the CarAgent component if it exists
        CarAgent agent = car.GetComponent<CarAgent>();
        if (agent != null)
        {
            agent.enabled = false;
        }
        
        // Disable any other AI related components
        // Add more components as needed based on your setup
    }
    
    void SetupCamera()
    {
        Camera cam = Camera.main;
        if (cam != null)
        {
            SimpleFollowCamera followScript = cam.gameObject.GetComponent<SimpleFollowCamera>();
            if (followScript == null)
            {
                followScript = cam.gameObject.AddComponent<SimpleFollowCamera>();
            }
            followScript.target = carInstance.transform;
            Debug.Log("Camera set to follow car");
        }
        else
        {
            Debug.LogError("No main camera found!");
        }
    }
    
    void Update()
    {
        if (carController != null)
        {
            // Get manual input
            float steering = Input.GetAxis("Horizontal");
            float throttle = Input.GetAxis("Vertical") > 0 ? Input.GetAxis("Vertical") : 0;
            float brake = Input.GetAxis("Vertical") < 0 ? -Input.GetAxis("Vertical") : 0;
            
            // Apply to car controller
            carController.Move(steering, throttle, brake);
        }
    }
    
    // Add reset functionality
    void OnGUI()
    {
        if (GUI.Button(new Rect(10, 10, 100, 30), "Reset Car"))
        {
            ResetCar();
        }
    }
    
    void ResetCar()
    {
        Destroy(carInstance);
        SpawnTestCar();
    }
}

// Simple camera follow script
public class SimpleFollowCamera : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0, 5, -10);
    
    void LateUpdate()
    {
        if (target != null)
        {
            transform.position = target.position + offset;
            transform.LookAt(target);
        }
    }
}