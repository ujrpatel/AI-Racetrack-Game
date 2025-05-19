using UnityEngine;

public class RewardDebugger : MonoBehaviour
{
    [HideInInspector] public float speedReward;
    [HideInInspector] public float alignmentReward;
    [HideInInspector] public float checkpointProgress;
    [HideInInspector] public float reversePenalty;
    [HideInInspector] public float circlingPenalty;
    [HideInInspector] public float wallProximity;
    [HideInInspector] public float total;
    [HideInInspector] public float projectedDistance;
    [HideInInspector] public float MAXprojectedDistance;
    [HideInInspector] public float GlobalprojectedDistance;
    [HideInInspector] public float lapTime;

    private GUIStyle style;

    private void Start()
    {
        style = new GUIStyle();
        style.normal.textColor = Color.black;
        style.fontSize = 14;
        style.alignment = TextAnchor.UpperLeft;
    }

    private void OnGUI()
    {
        // don’t draw any GUI when running in batch/headless
        if (Application.isBatchMode) return;

        var agent = GetComponentInParent<CarAgent>();
        if (agent == null || !agent.isMainAgent) return;

        string info = $@"
<b>REWARDS</b>
Progress: {checkpointProgress:F3}
<b>Total: {total:F3}</b>";
        GUI.Label(new Rect(10, 10, 300, 200), info, style);

        Rect btnRect = new Rect(10, 220, 120, 30);
        if (GUI.Button(btnRect, "Reset All Cars"))
        {
            var mgr = FindFirstObjectByType<TrainingManager>();
            if (mgr != null)
                mgr.ResetAllCars();
            else
                Debug.LogWarning("No TrainingManager found to reset cars.");
        }
    }
}
