// using UnityEngine;

// public class CheckpointTrigger : MonoBehaviour
// {
//     // Start is called once before the first execution of Update after the MonoBehaviour is created
//     void Start()
//     {
        
//     }
//     private bool isTriggered = false;
//     private void OnTriggerEnter(Collider other)
//     {
//         if (isTriggered) return;
//         if (other.CompareTag("Player")) // ✅ Make sure your car has the "Player" tag
//         {
//             isTriggered = true;
            
//             Debug.Log($"✅ Checkpoint Passed: {gameObject.name}");
//         }
//     }
//     // Update is called once per frame
//     void Update()
//     {
        
//     }
// }
using UnityEngine;

public class CheckpointTrigger : MonoBehaviour
{
    private CheckpointIdentifier identifier;
    private bool isTriggered = false;

    void Awake()
    {
        identifier = GetComponent<CheckpointIdentifier>();
    }

    void OnTriggerEnter(Collider other)
    {
        if (isTriggered) return;
        if (other.CompareTag("Player")) // Matches provided version
        {
            isTriggered = true;
            CarAgent agent = other.GetComponentInParent<CarAgent>();
            if (agent != null)
            {
                agent.OnCheckpointPassed(identifier.CheckpointIndex);
                Debug.Log($"✅ Checkpoint Passed: {gameObject.name} (Index: {identifier.CheckpointIndex})");
            }
        }
    }

    // Reset trigger state at the start of each episode
    public void ResetTrigger()
    {
        isTriggered = false;
    }
}