using UnityEngine;

[RequireComponent(typeof(Collider))]
public class Checkpoint : MonoBehaviour
{
    [SerializeField] private int checkpointIndex;

    public int CheckpointIndex => checkpointIndex;

    public void SetIndex(int index) => checkpointIndex = index;

    private void Awake()
    {
        // Ensure the checkpoint has a trigger collider
        Collider col = GetComponent<Collider>();
        if (col != null && !col.isTrigger)
        {
            col.isTrigger = true;
            Debug.LogWarning($"[Checkpoint {checkpointIndex}] Collider was not a trigger. Automatically set to isTrigger = true.");
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        CarAgent agent = other.GetComponentInParent<CarAgent>();
        if (agent != null)
        {
            agent.OnCheckpointPassed(checkpointIndex);
        }
    }
}
