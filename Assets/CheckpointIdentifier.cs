// using UnityEngine;

// public class CheckpointIdentifier : MonoBehaviour
// {
//     public int CheckpointIndex { get; set; }
    
//     void Awake()
//     {
//         // Make sure the gameObject has a collider with isTrigger = true
//         Collider col = GetComponent<Collider>();
//         if (col != null && !col.isTrigger)
//         {
//             Debug.LogWarning($"Checkpoint {gameObject.name} collider should be a trigger. Setting isTrigger=true");
//             col.isTrigger = true;
//         }
//     }
    
//     // void OnDrawGizmos()
//     // {
//     //     // Draw checkpoint number in scene view for easier identification
//     //     #if UNITY_EDITOR
//     //     UnityEditor.Handles.Label(transform.position + Vector3.up * 2, $"CP {CheckpointIndex}");
//     //     #endif
//     // }
// }

using UnityEngine;

public class CheckpointIdentifier : MonoBehaviour
{
    [SerializeField] private int checkpointIndex;
    
    public int CheckpointIndex
    {
        get { return checkpointIndex; }
        set { checkpointIndex = value; }
    }
    
    void Awake()
    {
        // Make sure it's a trigger
        Collider collider = GetComponent<Collider>();
        if (collider != null && !collider.isTrigger)
        {
            Debug.LogWarning($"Checkpoint {gameObject.name} collider should be a trigger. Setting isTrigger=true");
            collider.isTrigger = true;
        }
    }
    
    void OnDrawGizmos()
    {
        // Draw checkpoint number in scene view for easier identification
        #if UNITY_EDITOR
        UnityEditor.Handles.Label(transform.position + Vector3.up * 2, $"CP {checkpointIndex}");
        #endif
    }
}