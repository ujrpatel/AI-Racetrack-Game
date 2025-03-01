using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using UnityEngine.Splines;
using System.ComponentModel;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class SplineTrackGenerator : MonoBehaviour
{
    [Header("Spline Settings")]
    public SplineContainer splineContainer;
    public int splineIndex = 0;

    [Header("Track Settings")]
    public float roadWidth = 2f;
    public float wallHeight = 1f;
    public float wallThickness = 0.2f;
    public float wallOffset = 0.1f;
    public int resolution = 20;

    public Material roadMaterial;
    public Material wallMaterial;

    [Header("Checkpoint Settings")]
    public GameObject checkpointPrefab;
    public float checkpointSpacing = 1f;
    [SerializeField] public int totalCheckpoints;

    private Spline spline;
    private List<GameObject> checkpoints = new List<GameObject>();

    private void OnEnable()
    {
        ValidateSpline();
        GenerateTrack();
        GenerateCheckpoints();
    }

    private void Update()
    {
        if (!Application.isPlaying)
        {
            ValidateSpline();
            GenerateTrack();
            ClearExistingCheckpoints();
            GenerateCheckpoints();
        }
    }

    private void GenerateCheckpoints()
    {
        ClearExistingCheckpoints();
        if (checkpointPrefab ==null)
        {
            Debug.LogError("❌ ERROR: No Checkpoint Prefab assigned!");
            return;
        }

        float trackLength = SplineUtility.CalculateLength(splineContainer.Splines[splineIndex], float4x4.identity);
        totalCheckpoints = Mathf.FloorToInt(trackLength / checkpointSpacing);

        Transform checkpointsParent = transform.Find("Checkpoints");
        if (checkpointsParent == null)
        {
            GameObject newParent = new GameObject("Checkpoints");
            newParent.transform.SetParent(transform);
            checkpointsParent = newParent.transform;
        }

        for (int i = 0; i < totalCheckpoints; i++)
        {
            float t = i * checkpointSpacing / trackLength; // Normalize 0-1 along spline
            float3 position, forward, upVector;
            splineContainer.Evaluate(splineIndex, t, out position, out forward, out upVector);

            float3 right = math.normalize(math.cross(Vector3.up, forward));

            // Centered on track and spans the full width
            Vector3 center = position;
            Quaternion rotation = Quaternion.LookRotation(forward, Vector3.up);

            GameObject checkpoint = Instantiate(checkpointPrefab, center, rotation, checkpointsParent);
            checkpoint.name = $"Checkpoint_{i}";
            ConfigureCheckpoint(checkpoint, right);
            checkpoints.Add(checkpoint);
        }
    }
    private void ConfigureCheckpoint(GameObject checkpoint, float3 right)
{
    BoxCollider collider = checkpoint.GetComponent<BoxCollider>();
    if (collider == null)
        collider = checkpoint.AddComponent<BoxCollider>();

    collider.isTrigger = true; // Ensure it's a trigger

    // Use roadWidth from this script
    float checkpointWidth = roadWidth ; //
    float checkpointHeight = 3f;
    float checkpointLength = 2f;
    collider.size = new Vector3(checkpointWidth, checkpointHeight, checkpointLength);
    collider.center = Vector3.zero;

    Vector3 position = checkpoint.transform.position;
    position.y += checkpointHeight / 2;  // Move it up so the bottom aligns
    checkpoint.transform.position = position;
    // Adjust visual size if the checkpoint is a visible object
    // checkpoint.transform.localScale = new Vector3(checkpointWidth, checkpoint.transform.localScale.y, checkpoint.transform.localScale.z);
    // checkpoint.transform.localScale = new Vector3(checkpointWidth, checkpointHeight, checkpointLength);

    // Make invisible in the game
    MeshRenderer renderer = checkpoint.GetComponent<MeshRenderer>();
        if (renderer != null) renderer.enabled = false;
    // checkpoint.GetComponent<MeshRenderer>().enabled = false;

    if (checkpoint.GetComponent<CheckpointTrigger>() == null)
        {
            checkpoint.AddComponent<CheckpointTrigger>();
        }
}

    private void ClearExistingCheckpoints()
    {
        Transform checkpointsParent = transform.Find("Checkpoints");
        if (checkpointsParent != null)
        {
            // Destroy all children under the "Checkpoints" parent
            for (int i = checkpointsParent.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(checkpointsParent.GetChild(i).gameObject);
            }
        }

        checkpoints.Clear();
    }

    private void OnValidate()
    {
#if UNITY_EDITOR
        EditorApplication.delayCall += () => {
            if (this == null || gameObject == null) return;
            ValidateSpline();
            GenerateTrack();
        };
#endif
    }

    private void ValidateSpline()
    {
        if (splineContainer == null)
        {
            Debug.LogError("❌ ERROR: No SplineContainer assigned! Assign one in the Inspector.");
            return;
        }

        if (splineContainer.Splines == null || splineContainer.Splines.Count == 0)
        {
            Debug.LogError("❌ ERROR: The assigned SplineContainer has no splines!");
            return;
        }

        spline = splineContainer.Splines[splineIndex];
    }

    public void GenerateTrack()
    {
        if (this == null || gameObject == null) return;
        if (spline == null) return;

        GameObject roadObject = EnsureMeshObject("Road");
        roadObject.GetComponent<MeshFilter>().sharedMesh = CreateRoadMesh();
        roadObject.GetComponent<MeshCollider>().sharedMesh = roadObject.GetComponent<MeshFilter>().sharedMesh;

        MeshRenderer roadRenderer = roadObject.GetComponent<MeshRenderer>();
        if (roadRenderer.sharedMaterial == null)
            roadRenderer.sharedMaterial = roadMaterial;

        GameObject leftWall = EnsureMeshObject("LeftWall");
        GameObject rightWall = EnsureMeshObject("RightWall");

        leftWall.GetComponent<MeshFilter>().sharedMesh = CreateWallMesh(true);
        leftWall.GetComponent<MeshCollider>().sharedMesh = leftWall.GetComponent<MeshFilter>().sharedMesh;

        rightWall.GetComponent<MeshFilter>().sharedMesh = CreateWallMesh(false);
        rightWall.GetComponent<MeshCollider>().sharedMesh = rightWall.GetComponent<MeshFilter>().sharedMesh;

        MeshRenderer leftRenderer = leftWall.GetComponent<MeshRenderer>();
        MeshRenderer rightRenderer = rightWall.GetComponent<MeshRenderer>();

        if (leftRenderer.sharedMaterial == null)
            leftRenderer.sharedMaterial = wallMaterial;

        if (rightRenderer.sharedMaterial == null)
            rightRenderer.sharedMaterial = wallMaterial;
    }

    private Mesh CreateRoadMesh()
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();

        for (int i = 0; i <= resolution; i++)
        {
            float t = i / (float)resolution;
            float3 position, forward, upVector;
            splineContainer.Evaluate(splineIndex, t, out position, out forward, out upVector);

            float3 right = math.normalize(math.cross(Vector3.up, forward));

            Vector3 leftEdge = (Vector3)(position - (right * roadWidth * 0.5f));  // Flipped to match proper alignment
            Vector3 rightEdge = (Vector3)(position + (right * roadWidth * 0.5f));

            vertices.Add(leftEdge);
            vertices.Add(rightEdge);
            uvs.Add(new Vector2(0, t));
            uvs.Add(new Vector2(1, t));

            if (i < resolution)
            {
                int start = i * 2;
                triangles.Add(start);
                triangles.Add(start + 2);
                triangles.Add(start + 1);

                triangles.Add(start + 1);
                triangles.Add(start + 2);
                triangles.Add(start + 3);
            }
        }

        if (vertices.Count == 0) return null;

        Mesh mesh = new Mesh
        {
            vertices = vertices.ToArray(),
            triangles = triangles.ToArray(),
            uv = uvs.ToArray()
        };

        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();

        return mesh;
    }

    private Mesh CreateWallMesh(bool isLeftWall)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        for (int i = 0; i <= resolution; i++)
        {
            float t = i / (float)resolution;
            float3 position, forward, upVector;
            splineContainer.Evaluate(splineIndex, t, out position, out forward, out upVector);

            float3 right = math.normalize(math.cross(Vector3.up, forward));
            float sideMultiplier = isLeftWall ? -0.5f : 0.5f; // Fixed alignment

            Vector3 baseBottom = (Vector3)(position + (right * roadWidth * sideMultiplier));
            Vector3 wallBottom = baseBottom + (Vector3)(right * wallOffset);
            Vector3 wallTop = wallBottom + new Vector3(0, wallHeight, 0);
            Vector3 outerBottom = wallBottom + (Vector3)(right * wallThickness);
            Vector3 outerTop = wallTop + (Vector3)(right * wallThickness);

            vertices.Add(wallBottom);
            vertices.Add(wallTop);
            vertices.Add(outerBottom);
            vertices.Add(outerTop);

            if (i < resolution)
            {
                int start = i * 4;

                // Front Face (Facing Road)
                triangles.Add(start);
                triangles.Add(start + 1);
                triangles.Add(start + 2);

                triangles.Add(start + 1);
                triangles.Add(start + 3);
                triangles.Add(start + 2);

                // Top Face (Flat Surface)
                triangles.Add(start + 1);
                triangles.Add(start + 5);
                triangles.Add(start + 7);

                triangles.Add(start + 1);
                triangles.Add(start + 7);
                triangles.Add(start + 3);

                // Outer Face (Facing Outward)
                triangles.Add(start + 2);
                triangles.Add(start + 3);
                triangles.Add(start + 6);

                triangles.Add(start + 3);
                triangles.Add(start + 7);
                triangles.Add(start + 6);

                // Back Face (Inner Side of Wall)
                triangles.Add(start);
                triangles.Add(start + 2);
                triangles.Add(start + 4);

                triangles.Add(start + 2);
                triangles.Add(start + 6);
                triangles.Add(start + 4);

                // Bottom Face (Underside of Wall)
                triangles.Add(start);
                triangles.Add(start + 4);
                triangles.Add(start + 5);

                triangles.Add(start);
                triangles.Add(start + 5);
                triangles.Add(start + 1);
            }
        }

        if (vertices.Count == 0) return null;

        Mesh mesh = new Mesh
        {
            vertices = vertices.ToArray(),
            triangles = triangles.ToArray()
        };

        mesh.RecalculateNormals();
        return mesh;
    }

    private GameObject EnsureMeshObject(string name)
    {
        if (this == null || gameObject == null) return null;

        GameObject obj = transform.Find(name)?.gameObject;

        if (obj == null)
        {
            obj = new GameObject(name);
            obj.transform.SetParent(transform, false);
            obj.AddComponent<MeshFilter>();
            obj.AddComponent<MeshRenderer>();
            obj.AddComponent<MeshCollider>();
        }

        obj.transform.localPosition = Vector3.zero;
        obj.transform.localRotation = Quaternion.identity;

        return obj;
    }
}
