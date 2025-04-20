using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using UnityEngine.Splines;
using System.Reflection;

#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class SplineTrackGenerator : MonoBehaviour
{
    [Header("Spline Settings")]
    public SplineContainer splineContainer;
    public int splineIndex = 0;
    [Range(10, 500)]
    public int resolution = 100; // Detail level for mesh generation
    public float splineOffset = 0f; // Vertical offset to align with terrain

    [Header("Track Settings")]
    public float roadWidth = 10f;
    public float wallHeight = 3f;
    public float wallThickness = 0.2f;
    public float wallOffset = 0f; // Changed to 0 to ensure walls are flush with road
    public Material roadMaterial;
    public Material wallMaterial;
    
    [Header("Checkpoint Settings")]
    public int numberOfCheckpoints = 20;
    public Color startFinishColor = Color.red;
    public float startFinishLengthScale = 1.5f; // Only scales length, not width or height
    public float checkpointHeight = 3f;
    public float checkpointLength = 1f;
    public bool checkpointsVisible = false;

    // For tracking
    public List<Transform> checkpoints = new List<Transform>();
    private bool initialized = false;
    
    // Track metrics for display in inspector
    [HideInInspector] public float estimatedTrackLength = 0f;
    [HideInInspector] public float averageCheckpointSpacing = 0f;

    private bool isGenerating = false;
    private void OnEnable()
    {
        // Ensure tags exist
        CreateRequiredTags();
        
        // Initialization
        if (!initialized && !isGenerating)
        {
            ValidateSpline();
            if (splineContainer != null)
            {
                isGenerating = true;
                GenerateTrack();
                isGenerating = false;
                initialized = true;
            }
        }
    }

    private void CreateRequiredTags()
    {
        // Create tags in Editor (much more reliable than runtime checks)
#if UNITY_EDITOR
        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty tagsProp = tagManager.FindProperty("tags");

        AddTag(tagsProp, "Wall");
        AddTag(tagsProp, "Checkpoint");
        AddTag(tagsProp, "StartFinish");
        AddTag(tagsProp, "Road");

        tagManager.ApplyModifiedProperties();
#endif
    }

#if UNITY_EDITOR
    private void AddTag(SerializedProperty tagsProp, string newTag)
    {
        bool found = false;
        
        // Check if tag already exists
        for (int i = 0; i < tagsProp.arraySize; i++)
        {
            SerializedProperty t = tagsProp.GetArrayElementAtIndex(i);
            if (t.stringValue.Equals(newTag)) { found = true; break; }
        }
        
        // Add the tag if it doesn't exist
        if (!found)
        {
            tagsProp.arraySize++;
            SerializedProperty newTagProp = tagsProp.GetArrayElementAtIndex(tagsProp.arraySize - 1);
            newTagProp.stringValue = newTag;
            Debug.Log($"Added tag: {newTag}");
        }
    }
#endif

    private void OnValidate()
    {
#if UNITY_EDITOR
        EditorApplication.delayCall += () => {
            if (this == null || gameObject == null) return;
            ValidateSpline();
            if (splineContainer != null)
            {
                GenerateTrack();
            }
        };
#endif
    }

    private void ValidateSpline()
    {
        // Basic validation
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

        if (splineIndex >= splineContainer.Splines.Count)
        {
            Debug.LogError($"❌ ERROR: Spline index {splineIndex} is out of range (max: {splineContainer.Splines.Count - 1})");
            splineIndex = 0;
        }

        Debug.Log($"Validated spline with {splineContainer.Splines[splineIndex].Count} knots");
    }

    public void GenerateTrack()
    {
        if (splineContainer == null) 
        {
            Debug.LogError("Cannot generate track: SplineContainer is null");
            return;
        }

        if (splineIndex >= splineContainer.Splines.Count)
        {
            Debug.LogError("Cannot generate track: Invalid spline index");
            return;
        }

        // Calculate track length and metrics first
        CalculateTrackMetrics();
        
        // Generate road
        GenerateRoad();
        
        // Generate walls
        ClearExistingWalls();
        GenerateWalls();

        // Generate checkpoints
        ClearExistingCheckpoints();
        GenerateCheckpoints();
        
        Debug.Log("Track generation complete");
    }
    
    private void CalculateTrackMetrics()
    {
        if (splineContainer == null || splineIndex >= splineContainer.Splines.Count)
            return;
            
        float length = 0f;
        Vector3 prevPosition = Vector3.zero;
        bool first = true;
        
        // Calculate actual track length using points along the spline
        for (int i = 0; i <= resolution; i++)
        {
            float t = i / (float)resolution;
            
            float3 splinePosition, splineTangent, splineUp;
            if (SplineUtility.Evaluate(splineContainer.Splines[splineIndex], t, out splinePosition, out splineTangent, out splineUp))
            {
                Vector3 position = splineContainer.transform.TransformPoint(
                    new Vector3(splinePosition.x, splinePosition.y + splineOffset, splinePosition.z));
                
                if (!first)
                {
                    length += Vector3.Distance(prevPosition, position);
                }
                
                prevPosition = position;
                first = false;
            }
        }
        
        estimatedTrackLength = length;
        averageCheckpointSpacing = numberOfCheckpoints > 1 ? length / numberOfCheckpoints : 0;
    }
    
    private void GenerateRoad()
    {
        // Find or create Road object
        GameObject roadObject = transform.Find("Road")?.gameObject;
        if (roadObject == null)
        {
            roadObject = new GameObject("Road");
            roadObject.transform.SetParent(transform);
            roadObject.transform.localPosition = Vector3.zero;
            roadObject.transform.localRotation = Quaternion.identity;
        }
        
        // Tag as road
        roadObject.tag = "Road";
        
        // Add mesh components if they don't exist
        MeshFilter meshFilter = roadObject.GetComponent<MeshFilter>();
        if (meshFilter == null)
            meshFilter = roadObject.AddComponent<MeshFilter>();
            
        MeshRenderer meshRenderer = roadObject.GetComponent<MeshRenderer>();
        if (meshRenderer == null)
            meshRenderer = roadObject.AddComponent<MeshRenderer>();
            
        MeshCollider meshCollider = roadObject.GetComponent<MeshCollider>();
        if (meshCollider == null)
            meshCollider = roadObject.AddComponent<MeshCollider>();
        
        // Create road mesh
        Mesh roadMesh = CreateRoadMesh();
        
        // Apply mesh
        meshFilter.sharedMesh = roadMesh;
        meshCollider.sharedMesh = roadMesh;
        
        // Apply material
        if (roadMaterial != null)
        {
            meshRenderer.sharedMaterial = roadMaterial;
        }
        else if (meshRenderer.sharedMaterial == null)
        {
            // Create a default material if none is assigned
            Material defaultMaterial = new Material(Shader.Find("Standard"));
            defaultMaterial.color = Color.gray;
            meshRenderer.sharedMaterial = defaultMaterial;
        }
        
        Debug.Log("Generated road mesh");
    }

    private Mesh CreateRoadMesh()
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Vector2> uvs = new List<Vector2>();
        
        float totalDistance = 0f;
        Vector3 prevPosition = Vector3.zero;
        bool first = true;
        
        // Calculate transformations for accurate spline evaluation
        float4x4 splineToWorld = splineContainer.transform.localToWorldMatrix;
        float4x4 worldToLocal = transform.worldToLocalMatrix;
        
        // Generate vertices along the spline
        for (int i = 0; i <= resolution; i++)
        {
            float t = i / (float)resolution;
            
            // Get spline point in world space with proper transform
            Vector3 position;
            Vector3 tangent;
            
            try
            {
                float3 splinePosition, splineTangent, splineUp;
                
                // Evaluate in spline space
                if (SplineUtility.Evaluate(splineContainer.Splines[splineIndex], t, out splinePosition, out splineTangent, out splineUp))
                {
                    // Convert to world space
                    Vector3 worldPos = splineContainer.transform.TransformPoint(new Vector3(splinePosition.x, splinePosition.y + splineOffset, splinePosition.z));
                    Vector3 worldTangent = splineContainer.transform.TransformDirection(new Vector3(splineTangent.x, splineTangent.y, splineTangent.z));
                    
                    // Convert to local space of this object
                    position = transform.InverseTransformPoint(worldPos);
                    tangent = transform.InverseTransformDirection(worldTangent);
                }
                else
                {
                    // Fallback if evaluation fails
                    position = splineContainer.transform.TransformPoint(new Vector3(0, splineOffset, 0));
                    tangent = Vector3.forward;
                }
            }
            catch
            {
                Debug.LogWarning($"Failed to evaluate spline at t={t}");
                continue;
            }
            
            // Calculate distance for UV mapping
            if (!first)
            {
                totalDistance += Vector3.Distance(prevPosition, position);
            }
            prevPosition = position;
            first = false;
            
            // Calculate road edges
            Vector3 up = Vector3.up;
            Vector3 right = Vector3.Cross(up, tangent.normalized).normalized;
            
            Vector3 leftEdge = position - right * (roadWidth / 2);
            Vector3 rightEdge = position + right * (roadWidth / 2);
            
            // Add vertices
            vertices.Add(leftEdge);
            vertices.Add(rightEdge);
            
            // Add UVs - map based on distance and road width
            float uv_y = totalDistance / 10f; // Scale UV based on distance
            uvs.Add(new Vector2(0, uv_y));
            uvs.Add(new Vector2(1, uv_y));
            
            // Add triangles (except for last iteration)
            if (i < resolution)
            {
                int baseIndex = i * 2;
                
                // First triangle
                triangles.Add(baseIndex);
                triangles.Add(baseIndex + 2);
                triangles.Add(baseIndex + 1);
                
                // Second triangle
                triangles.Add(baseIndex + 1);
                triangles.Add(baseIndex + 2);
                triangles.Add(baseIndex + 3);
            }
        }
        
        // Create and configure mesh
        Mesh mesh = new Mesh();
        mesh.vertices = vertices.ToArray();
        mesh.triangles = triangles.ToArray();
        mesh.uv = uvs.ToArray();
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        
        return mesh;
    }

    private void ClearExistingWalls()
    {
        // Find and clear wall objects
        Transform leftWall = transform.Find("LeftWall");
        Transform rightWall = transform.Find("RightWall");
        
        if (leftWall != null)
        {
            if (Application.isPlaying)
                Destroy(leftWall.gameObject);
            else
                DestroyImmediate(leftWall.gameObject);
        }
        
        if (rightWall != null)
        {
            if (Application.isPlaying)
                Destroy(rightWall.gameObject);
            else
                DestroyImmediate(rightWall.gameObject);
        }
        
        Debug.Log("Cleared existing walls");
    }

    private void GenerateWalls()
    {
        // Create left and right walls
        GameObject leftWallObj = new GameObject("LeftWall");
        leftWallObj.transform.SetParent(transform);
        leftWallObj.transform.localPosition = Vector3.zero;
        leftWallObj.tag = "Wall";
        
        GameObject rightWallObj = new GameObject("RightWall");
        rightWallObj.transform.SetParent(transform);
        rightWallObj.transform.localPosition = Vector3.zero;
        rightWallObj.tag = "Wall";
        
        // Add mesh components to walls
        MeshFilter leftMeshFilter = leftWallObj.AddComponent<MeshFilter>();
        MeshRenderer leftMeshRenderer = leftWallObj.AddComponent<MeshRenderer>();
        MeshCollider leftMeshCollider = leftWallObj.AddComponent<MeshCollider>();
        
        MeshFilter rightMeshFilter = rightWallObj.AddComponent<MeshFilter>();
        MeshRenderer rightMeshRenderer = rightWallObj.AddComponent<MeshRenderer>();
        MeshCollider rightMeshCollider = rightWallObj.AddComponent<MeshCollider>();
        
        // Create wall meshes
        Mesh leftWallMesh = CreateWallMesh(true);
        Mesh rightWallMesh = CreateWallMesh(false);
        
        // Apply meshes
        leftMeshFilter.sharedMesh = leftWallMesh;
        leftMeshCollider.sharedMesh = leftWallMesh;
        
        rightMeshFilter.sharedMesh = rightWallMesh;
        rightMeshCollider.sharedMesh = rightWallMesh;
        
        // Apply materials
        if (wallMaterial != null)
        {
            leftMeshRenderer.sharedMaterial = wallMaterial;
            rightMeshRenderer.sharedMaterial = wallMaterial;
        }
        else
        {
            // Default material if none assigned
            Material defaultWallMaterial = new Material(Shader.Find("Standard"));
            defaultWallMaterial.color = Color.gray;
            
            leftMeshRenderer.sharedMaterial = defaultWallMaterial;
            rightMeshRenderer.sharedMaterial = defaultWallMaterial;
        }
        
        Debug.Log("Generated wall meshes");
    }

    private Mesh CreateWallMesh(bool isLeftWall)
    {
        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();

        for (int i = 0; i <= resolution; i++)
        {
            float t = i / (float)resolution;
            float3 position, forward, upVector;
            
            try {
                // Evaluate the spline at this point
                if (SplineUtility.Evaluate(splineContainer.Splines[splineIndex], t, out position, out forward, out upVector))
                {
                    // Add spline offset
                    position.y += splineOffset;
                    
                    // Calculate direction perpendicular to the spline
                    Vector3 worldPosition = splineContainer.transform.TransformPoint((Vector3)position);
                    Vector3 worldForward = splineContainer.transform.TransformDirection((Vector3)forward);
                    Vector3 worldUp = Vector3.up;
                    Vector3 worldRight = Vector3.Cross(worldUp, worldForward).normalized;
                    
                    // Transform back to local space for mesh creation
                    Vector3 localPosition = transform.InverseTransformPoint(worldPosition);
                    Vector3 localRight = transform.InverseTransformDirection(worldRight);
                    
                    // Determine the wall position (at the exact edge of the road)
                    float sideMultiplier = isLeftWall ? -0.5f : 0.5f;
                    Vector3 roadEdge = localPosition + localRight * (roadWidth * sideMultiplier);
                    
                    // Wall bottom vertices (exactly at road edge)
                    Vector3 wallBottom = roadEdge;
                    Vector3 wallTop = wallBottom + Vector3.up * wallHeight;
                    
                    // Outside wall vertices
                    Vector3 wallDirection = isLeftWall ? -localRight : localRight;
                    Vector3 outerBottom = wallBottom + wallDirection * wallThickness;
                    Vector3 outerTop = wallTop + wallDirection * wallThickness;
                    
                    // Add vertices in same order as original
                    vertices.Add(wallBottom);     // Inner bottom
                    vertices.Add(wallTop);        // Inner top
                    vertices.Add(outerBottom);    // Outer bottom
                    vertices.Add(outerTop);       // Outer top
                    
                    // Add triangles (when not the last point)
                    if (i < resolution)
                    {
                        int start = i * 4;
                        
                        if (isLeftWall)
                        {
                            // Front Face (Facing Road)
                            triangles.Add(start);
                            triangles.Add(start + 2);
                            triangles.Add(start + 1);
                            
                            triangles.Add(start + 1);
                            triangles.Add(start + 2);
                            triangles.Add(start + 3);
                            
                            // Top Face
                            triangles.Add(start + 1);
                            triangles.Add(start + 7);
                            triangles.Add(start + 5);
                            
                            triangles.Add(start + 1);
                            triangles.Add(start + 3);
                            triangles.Add(start + 7);
                            
                            // Outer Face
                            triangles.Add(start + 2);
                            triangles.Add(start + 6);
                            triangles.Add(start + 3);
                            
                            triangles.Add(start + 3);
                            triangles.Add(start + 6);
                            triangles.Add(start + 7);
                            
                            // Back Face
                            triangles.Add(start);
                            triangles.Add(start + 4);
                            triangles.Add(start + 2);
                            
                            triangles.Add(start + 2);
                            triangles.Add(start + 4);
                            triangles.Add(start + 6);
                            
                            // Bottom Face
                            triangles.Add(start);
                            triangles.Add(start + 5);
                            triangles.Add(start + 4);
                            
                            triangles.Add(start);
                            triangles.Add(start + 1);
                            triangles.Add(start + 5);
                        }
                        else
                        {
                            // Right wall - keep original triangle ordering
                            
                            // Front Face (Facing Road)
                            triangles.Add(start);
                            triangles.Add(start + 1);
                            triangles.Add(start + 2);
                            
                            triangles.Add(start + 1);
                            triangles.Add(start + 3);
                            triangles.Add(start + 2);
                            
                            // Top Face
                            triangles.Add(start + 1);
                            triangles.Add(start + 5);
                            triangles.Add(start + 7);
                            
                            triangles.Add(start + 1);
                            triangles.Add(start + 7);
                            triangles.Add(start + 3);
                            
                            // Outer Face
                            triangles.Add(start + 2);
                            triangles.Add(start + 3);
                            triangles.Add(start + 6);
                            
                            triangles.Add(start + 3);
                            triangles.Add(start + 7);
                            triangles.Add(start + 6);
                            
                            // Back Face
                            triangles.Add(start);
                            triangles.Add(start + 2);
                            triangles.Add(start + 4);
                            
                            triangles.Add(start + 2);
                            triangles.Add(start + 6);
                            triangles.Add(start + 4);
                            
                            // Bottom Face
                            triangles.Add(start);
                            triangles.Add(start + 4);
                            triangles.Add(start + 5);
                            
                            triangles.Add(start);
                            triangles.Add(start + 5);
                            triangles.Add(start + 1);
                        }
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"Error creating wall mesh at t={t}: {e.Message}");
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

    private void ClearExistingCheckpoints()
    {
        // Clear checkpoint references
        checkpoints.Clear();
        
        // Find and clear Checkpoints parent
        Transform checkpointsParent = transform.Find("Checkpoints");
        if (checkpointsParent != null)
        {
            if (Application.isPlaying)
                Destroy(checkpointsParent.gameObject);
            else
                DestroyImmediate(checkpointsParent.gameObject);
        }
        
        Debug.Log("Cleared existing checkpoints");
    }

    private void GenerateCheckpoints()
    {
        // Create Checkpoints parent
        Transform checkpointsParent = transform.Find("Checkpoints");
        if (checkpointsParent == null)
        {
            GameObject checkpointsObj = new GameObject("Checkpoints");
            checkpointsObj.transform.SetParent(transform);
            checkpointsParent = checkpointsObj.transform;
        }
        
        // Calculate spacing between checkpoints
        float spacing = 1.0f / numberOfCheckpoints;
        
        Debug.Log($"Generating {numberOfCheckpoints} checkpoints with spacing {spacing}");
        
        for (int i = 0; i < numberOfCheckpoints; i++)
        {
            // Calculate position along spline (0-1)
            float t = i * spacing;
            
            // Get spline point in world space with proper transform
            Vector3 position;
            Vector3 tangent;
            
            try
            {
                float3 splinePosition, splineTangent, splineUp;
                
                // Evaluate in spline space
                if (SplineUtility.Evaluate(splineContainer.Splines[splineIndex], t, out splinePosition, out splineTangent, out splineUp))
                {
                    // Convert to world space
                    Vector3 worldPos = splineContainer.transform.TransformPoint(new Vector3(splinePosition.x, splinePosition.y + splineOffset, splinePosition.z));
                    Vector3 worldTangent = splineContainer.transform.TransformDirection(new Vector3(splineTangent.x, splineTangent.y, splineTangent.z));
                    
                    // Convert to local space of this object for checkpoint placement
                    position = worldPos; // Keep in world space for checkpoint
                    tangent = worldTangent;
                }
                else
                {
                    // Fallback if evaluation fails
                    position = splineContainer.transform.TransformPoint(new Vector3(0, splineOffset, 0));
                    tangent = Vector3.forward;
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to evaluate spline at {t}: {e.Message}");
                continue;
            }
            
            Quaternion rotation = Quaternion.LookRotation(tangent, Vector3.up);
            
            // Create checkpoint
            GameObject checkpointObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            checkpointObj.transform.SetParent(checkpointsParent);
            checkpointObj.transform.position = position;
            checkpointObj.transform.rotation = rotation;
            
            // Scale checkpoint to match road width and specified dimensions
            checkpointObj.transform.localScale = new Vector3(roadWidth, checkpointHeight, checkpointLength);
            
            // Center checkpoint vertically (bottom at ground level)
            checkpointObj.transform.position += Vector3.up * (checkpointHeight / 2);
            
            // Make it a trigger
            BoxCollider collider = checkpointObj.GetComponent<BoxCollider>();
            if (collider != null)
            {
                collider.isTrigger = true;
            }
            
            // Make invisible if specified
            Renderer renderer = checkpointObj.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.enabled = checkpointsVisible;
                
                // Create a new material to avoid affecting other objects
                Material checkpointMaterial = new Material(Shader.Find("Standard"));
                renderer.sharedMaterial = checkpointMaterial;
            }
            
            checkpointObj.name = $"Checkpoint_{i}";
            
            // Add checkpoint identifier component
            CheckpointIdentifier identifier = checkpointObj.AddComponent<CheckpointIdentifier>();
            identifier.CheckpointIndex = i;

            checkpointObj.AddComponent<CheckpointTrigger>();
            
            // Special handling for start/finish line (checkpoint 0)
            if (i == 0)
            {
                checkpointObj.tag = "StartFinish";
                
                // Make start/finish checkpoint more distinctive if visible
                if (renderer != null && checkpointsVisible)
                {
                    renderer.sharedMaterial.color = startFinishColor;
                }
                
                // Only scale the length, not width or height
                checkpointObj.transform.localScale = new Vector3(
                    roadWidth, // Keep width the same
                    checkpointHeight, // Keep height the same
                    checkpointLength * startFinishLengthScale // Scale only length
                );
                
                Debug.Log($"Created start/finish line at position {position}");
            }
            else
            {
                checkpointObj.tag = "Checkpoint";
            }
            
            // Add to list
            checkpoints.Add(checkpointObj.transform);
        }
        
        Debug.Log($"Generated {checkpoints.Count} checkpoints");
    }

    public List<Vector3> GetTrackPoints()
    {
        List<Vector3> points = new List<Vector3>();
        
        if (splineContainer == null || splineIndex >= splineContainer.Splines.Count)
            return points;
        
        // Sample points along the spline
        int sampleCount = 100; // Number of points to sample
        
        for (int i = 0; i <= sampleCount; i++)
        {
            float t = i / (float)sampleCount;
            
            try
            {
                float3 splinePosition, splineTangent, splineUp;
                
                // Evaluate in spline space
                if (SplineUtility.Evaluate(splineContainer.Splines[splineIndex], t, out splinePosition, out splineTangent, out splineUp))
                {
                    // Convert to world space
                    Vector3 worldPos = splineContainer.transform.TransformPoint(
                        new Vector3(splinePosition.x, splinePosition.y + splineOffset, splinePosition.z));
                    
                    points.Add(worldPos);
                }
            }
            catch
            {
                // Skip on error
                continue;
            }
        }
        
        return points;
    }

    // Public getter for checkpoints - needed for LapTrackingSystem
    public List<Transform> GetCheckpoints()
    {
        return checkpoints;
    }

    // For debugging: Draw the spline in the editor
    void OnDrawGizmos()
    {
        if (splineContainer == null || splineContainer.Splines == null || 
            splineIndex >= splineContainer.Splines.Count) return;
        
        // Draw track width indicators along the spline
        Gizmos.color = Color.yellow;
        int steps = 20;
        
        for (int i = 0; i < steps; i++)
        {
            float t = (float)i / steps;
            
            // Get spline point in world space with proper transform
            Vector3 position;
            Vector3 tangent;
            
            try
            {
                float3 splinePosition, splineTangent, splineUp;
                
                // Evaluate in spline space
                if (SplineUtility.Evaluate(splineContainer.Splines[splineIndex], t, out splinePosition, out splineTangent, out splineUp))
                {
                    // Convert to world space
                    position = splineContainer.transform.TransformPoint(new Vector3(splinePosition.x, splinePosition.y + splineOffset, splinePosition.z));
                    tangent = splineContainer.transform.TransformDirection(new Vector3(splineTangent.x, splineTangent.y, splineTangent.z));
                }
                else
                {
                    continue;
                }
            }
            catch
            {
                continue; // Skip this step if evaluation fails
            }
            
            Vector3 up = Vector3.up;
            Vector3 right = Vector3.Cross(up, tangent.normalized).normalized;
            
            // Draw the width of the road
            Vector3 leftEdge = position - right * (roadWidth / 2);
            Vector3 rightEdge = position + right * (roadWidth / 2);
            
            Gizmos.DrawLine(leftEdge, rightEdge);
            
            // Draw wall positions
            Gizmos.color = Color.red;
            Gizmos.DrawSphere(leftEdge, 0.2f);
            Gizmos.DrawSphere(rightEdge, 0.2f);
            Gizmos.color = Color.yellow;
        }
        
        // Draw checkpoint positions
        Gizmos.color = Color.green;
        
        foreach (Transform checkpoint in checkpoints)
        {
            if (checkpoint != null)
            {
                Gizmos.DrawSphere(checkpoint.position, 0.5f);
            }
        }
    }
    
    #if UNITY_EDITOR
    [CustomEditor(typeof(SplineTrackGenerator))]
    public class SplineTrackGeneratorEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            
            SplineTrackGenerator generator = (SplineTrackGenerator)target;
            
            // Display track metrics
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Track Metrics", EditorStyles.boldLabel);
            
            // Calculate metrics if not already done
            if (generator.estimatedTrackLength <= 0)
            {
                generator.CalculateTrackMetrics();
            }
            
            EditorGUILayout.LabelField($"Estimated Track Length: {generator.estimatedTrackLength:F2} units");
            EditorGUILayout.LabelField($"Number of Checkpoints: {generator.numberOfCheckpoints}");
            EditorGUILayout.LabelField($"Checkpoint Spacing: {generator.averageCheckpointSpacing:F2} units");
            
            EditorGUILayout.Space();
            
            if (GUILayout.Button("Generate Track"))
            {
                generator.GenerateTrack();
            }
            
            if (GUILayout.Button("Clear Track"))
            {
                // Find the Road object and clear it
                Transform roadTransform = generator.transform.Find("Road");
                if (roadTransform != null)
                {
                    if (Application.isPlaying)
                        Destroy(roadTransform.gameObject);
                    else
                        DestroyImmediate(roadTransform.gameObject);
                }
                
                // Clear walls and checkpoints
                generator.ClearExistingWalls();
                generator.ClearExistingCheckpoints();
            }
            
            EditorGUILayout.HelpBox("The required tags are now automatically created.", MessageType.Info);
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Alignment Help", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("If the road/walls don't align with your spline, adjust the 'Spline Offset' value.", MessageType.Info);
            
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Wall Placement", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Walls are now placed flush with the edges of the road without gaps.", MessageType.Info);
        }
    }
    #endif
}