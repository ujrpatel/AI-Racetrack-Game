using UnityEngine;
using VehicleBehaviour;

public class SteeringDebugUI : MonoBehaviour
{
    public WheelVehicle vehicle;

    private void OnGUI()
    {
        if (vehicle == null)
        {
            GUI.Label(new Rect(10, 10, 300, 30), "[DEBUG] No vehicle assigned.");
            return;
        }

        // Read current values
        float steer = Mathf.Clamp(vehicle.Steering, -1f, 1f);
        float throttle = Mathf.Clamp(vehicle.Throttle, -1f, 1f);

        // STEERING BAR (horizontal top-center)
        float barWidth = 200f;
        float barHeight = 20f;
        float barX = (Screen.width - barWidth) / 2f;
        float barY = 10f;
        GUI.Box(new Rect(barX, barY, barWidth, barHeight), ""); // Background

        float steerNormalized = (steer + 1f) / 2f;
        float steerX = barX + steerNormalized * barWidth - 5f;
        GUI.Box(new Rect(steerX, barY, 10f, barHeight), "•");

        // THROTTLE BAR (vertical top-right)
        float tBarHeight = 200f;
        float tBarWidth = 20f;
        float tBarX = Screen.width - tBarWidth - 10f;
        float tBarY = 10f;
        GUI.Box(new Rect(tBarX, tBarY, tBarWidth, tBarHeight), ""); // Background

        float throttleNormalized = (throttle + 1f) / 2f;
        float throttleY = tBarY + (1f - throttleNormalized) * tBarHeight - 5f;
        GUI.Box(new Rect(tBarX, throttleY, tBarWidth, 10f), "•");

        // Labels (for quick testing)
        GUI.Label(new Rect(barX, barY + 25f, 150f, 20f), $"Steer: {steer:F2}");
        GUI.Label(new Rect(tBarX - 100f, tBarY + tBarHeight + 5f, 150f, 20f), $"Throttle: {throttle:F2}");
    }
}
