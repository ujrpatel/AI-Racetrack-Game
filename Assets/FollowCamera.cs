using UnityEngine;

public class FollowCamera : MonoBehaviour {
    public Transform target; // The car to follow
    public Vector3 offset = new Vector3(0f, 5f, -10f); // Camera offset from the car
    public float smoothSpeed = 0.125f; // Smoothing factor for camera movement

    void LateUpdate() {
        if (target == null) {
            Debug.LogWarning("[FollowCamera] Target is not assigned!");
            return;
        }

        Debug.Log("[FollowCamera] Following target at position: " + target.position);

        Vector3 desiredPosition = target.position + target.TransformDirection(offset);
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed);
        transform.position = smoothedPosition;

        transform.LookAt(target);
    }
}