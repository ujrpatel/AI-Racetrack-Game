
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
        // #if UNITY_EDITOR
        // UnityEditor.Handles.Label(transform.position + Vector3.up * 2, $"CP {checkpointIndex}");
        // #endif
    }
}

// using UnityEngine;

// public class CheckpointIdentifier : MonoBehaviour
// {
//     [Tooltip("Unique index for this checkpoint in the track")]
//     public int CheckpointIndex;
    
//     // Visual options
//     public Color checkpointColor = Color.blue;
//     public bool showDebugVisual = true;
    
//     void Start()
//     {
//         // Set tag if not already set
//         if (string.IsNullOrEmpty(gameObject.tag) || gameObject.tag == "Untagged")
//         {
//             gameObject.tag = "Checkpoint";
//         }
        
//         // Ensure there's a collider with trigger enabled
//         Collider collider = GetComponent<Collider>();
//         if (collider == null)
//         {
//             collider = gameObject.AddComponent<BoxCollider>();
//             collider.isTrigger = true;
//         }
//         else if (!collider.isTrigger)
//         {
//             collider.isTrigger = true;
//         }
        
//         // Apply color to any renderers
//         if (showDebugVisual)
//         {
//             Renderer renderer = GetComponent<Renderer>();
//             if (renderer != null)
//             {
//                 renderer.material.color = checkpointColor;
//             }
//         }
//     }
    
//     void OnDrawGizmos()
//     {
//         if (showDebugVisual)
//         {
//             Gizmos.color = checkpointColor;
//             Gizmos.DrawWireCube(transform.position, new Vector3(10, 3, 1));
            
//             // Draw text for checkpoint number
//             #if UNITY_EDITOR
//             UnityEditor.Handles.Label(transform.position + Vector3.up * 2, $"CP {CheckpointIndex}");
//             #endif
//         }
//     }
// }