using UnityEngine;

public class CheckpointTrigger : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }
    private bool isTriggered = false;
    private void OnTriggerEnter(Collider other)
    {
        if (isTriggered) return;
        if (other.CompareTag("Player")) // ✅ Make sure your car has the "Player" tag
        {
            isTriggered = true;
            
            Debug.Log($"✅ Checkpoint Passed: {gameObject.name}");
        }
    }
    // Update is called once per frame
    void Update()
    {
        
    }
}
