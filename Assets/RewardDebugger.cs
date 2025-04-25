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
        string info = $@"
<b>REWARDS</b>
Speed: {speedReward:F3}
Align: {alignmentReward:F3}
Progress: {checkpointProgress:F3}
Projected distance: {projectedDistance:F3}
Max Projected distance: {MAXprojectedDistance:F3}
GBL Projected distance: {GlobalprojectedDistance:F3}
Reverse: {reversePenalty:F3}
Circling: {circlingPenalty:F3}
Wall: {wallProximity:F3}
<b>Total: {total:F3}</b>";

        GUI.Label(new Rect(10, 10, 300, 200), info, style);
    }
}
