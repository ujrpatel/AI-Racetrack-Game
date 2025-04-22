using UnityEngine;
using VehicleBehaviour;

public class SteeringDebugUI : MonoBehaviour
{
    [Header("Debug Target")]
    public WheelVehicle targetVehicle;

    [Header("UI Settings")]
    public Vector2 screenPosition = new Vector2(0.5f, 0.95f); // Top center
    public float width = 200f;
    public float height = 10f;
    public Color barColor = Color.gray;
    public Color pointerColor = Color.cyan;

    private Texture2D texture;

    void Start()
    {
        texture = new Texture2D(1, 1);
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
    }

    void OnGUI()
    {
        if (targetVehicle == null) return;

        float steerNormalized = Mathf.Clamp(targetVehicle.SteerAngle / 50f, -1f, 1f); // Normalize assuming 50° max

        float x = Screen.width * screenPosition.x - width / 2;
        float y = Screen.height * (1 - screenPosition.y);

        // Draw background bar
        GUI.color = barColor;
        GUI.DrawTexture(new Rect(x, y, width, height), texture);

        // Draw center line
        GUI.color = Color.black;
        GUI.DrawTexture(new Rect(x + width / 2 - 1, y, 2, height), texture);

        // Draw pointer
        float pointerX = x + (width / 2) + (steerNormalized * (width / 2)) - 2;
        GUI.color = pointerColor;
        GUI.DrawTexture(new Rect(pointerX, y - 4, 4, height + 8), texture);

        GUI.color = Color.white; // Reset color
    }
}
