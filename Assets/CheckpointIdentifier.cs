// File: CheckpointIdentifier.cs
// Create this file if it doesn't exist

using UnityEngine;

public class CheckpointIdentifier : MonoBehaviour
{
    public int CheckpointIndex { get; set; }
    
    void Awake()
    {
        // Make sure the gameObject has a collider with isTrigger = true
        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            Debug.LogWarning($"Checkpoint {gameObject.name} collider should be a trigger. Setting isTrigger=true");
            col.isTrigger = true;
        }
    }
}