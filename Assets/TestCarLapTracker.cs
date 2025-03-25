using UnityEngine;
public class TestCarLapTracker : MonoBehaviour
{
    public LapTrackingSystem trackingSystem;
    private int checkpointsPassed = 0;
    private int currentLap = 0;
    
    // This displays HUD information during gameplay
    void OnGUI()
    {
        GUI.Label(new Rect(10, 40, 300, 20), $"Checkpoints: {checkpointsPassed}");
        GUI.Label(new Rect(10, 60, 300, 20), $"Lap: {currentLap}");
        
        if (trackingSystem != null)
        {
            CarAgent agent = GetComponent<CarAgent>();
            if (agent != null)
            {
                float completion = trackingSystem.GetLapCompletion(agent) * 100f;
                GUI.Label(new Rect(10, 80, 300, 20), $"Track Completion: {completion:F1}%");
            }
        }
    }
    
    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Checkpoint"))
        {
            checkpointsPassed++;
            Debug.Log($"Test car passed checkpoint. Total: {checkpointsPassed}");
        }
        else if (other.CompareTag("StartFinish"))
        {
            // Only increment lap if we've passed some checkpoints
            if (checkpointsPassed > 0)
            {
                currentLap++;
                Debug.Log($"TEST CAR COMPLETED LAP {currentLap}!");
                checkpointsPassed = 0;
            }
            else
            {
                Debug.Log("Crossed start/finish but not enough checkpoints passed");
            }
        }
    }
}
