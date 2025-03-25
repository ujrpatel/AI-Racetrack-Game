using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public class LapTimeRecord
{
    public int lapNumber;
    public float lapTime;
    public string agentName;
}

public class LapTrackingSystem : MonoBehaviour
{
    // Start/Finish line
    public Transform startFinishLine;
    public float startFinishLineWidth = 10f;
    public GameObject startFinishPrefab;
    public Color startFinishColor = Color.red;
    
    // Lap tracking
    private Dictionary<string, AgentLapData> agentLapData = new Dictionary<string, AgentLapData>();
    private List<LapTimeRecord> allLapTimeRecords = new List<LapTimeRecord>();
    
    // Reference to track generator for checkpoint count
    public SplineTrackGenerator trackGenerator;
    
    // Checkpoint completion requirements
    [Range(0.5f, 1.0f)]
    public float requiredCheckpointCompletion = 0.8f; // Require 80% of checkpoints instead of 100%
    
    // Debug settings
    public bool showDebugMessages = true;
    
    private class AgentLapData
    {
        public string agentId;
        public int currentLap = 0;
        public float lapStartTime = 0;
        public float bestLapTime = float.MaxValue;
        public HashSet<int> visitedCheckpoints = new HashSet<int>();
        public bool eligibleForLapCompletion = false;
        public Vector3 lastPosition;
        public float totalLapDistance = 0f; // Track distance for lap
    }
    
    void Start()
    {
        if (startFinishLine == null && trackGenerator != null)
        {
            // Create start/finish line at the first checkpoint
            CreateStartFinishLine();
        }
    }
    
    private void CreateStartFinishLine()
    {
        var checkpoints = trackGenerator.GetCheckpoints();
        if (checkpoints == null || checkpoints.Count == 0) return;
        
        // Use the first checkpoint as the start/finish line
        Transform firstCheckpoint = checkpoints[0];
        
        // Create a visual representation of the start/finish line
        if (startFinishPrefab != null)
        {
            GameObject startFinishObject = Instantiate(startFinishPrefab, firstCheckpoint.position, firstCheckpoint.rotation);
            startFinishObject.name = "StartFinishLine";
            startFinishObject.transform.parent = transform;
            
            // Set color if the object has a renderer
            Renderer renderer = startFinishObject.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = startFinishColor;
            }
            
            // Scale to track width
            startFinishObject.transform.localScale = new Vector3(
                startFinishLineWidth, 
                startFinishObject.transform.localScale.y, 
                startFinishObject.transform.localScale.z * 3f); // Make it more visible
            
            // Add a special tag to this checkpoint
            startFinishObject.tag = "StartFinish";
            
            // Set reference
            startFinishLine = startFinishObject.transform;
            
            // Make sure it has a collider with trigger enabled
            Collider collider = startFinishObject.GetComponent<Collider>();
            if (collider == null)
            {
                collider = startFinishObject.AddComponent<BoxCollider>();
            }
            collider.isTrigger = true;
        }
    }
    
    public void RegisterAgent(CarAgent agent)
    {
        string agentId = agent.gameObject.GetInstanceID().ToString();
        if (!agentLapData.ContainsKey(agentId))
        {
            agentLapData[agentId] = new AgentLapData
            {
                agentId = agentId,
                lapStartTime = Time.time,
                lastPosition = agent.transform.position
            };
            
            if (showDebugMessages)
            {
                Debug.Log($"Agent {agent.name} registered with Lap Tracking System");
            }
        }
    }
    
    public void UnregisterAgent(CarAgent agent)
    {
        string agentId = agent.gameObject.GetInstanceID().ToString();
        if (agentLapData.ContainsKey(agentId))
        {
            agentLapData.Remove(agentId);
        }
    }
    
    void Update()
    {
        // Update position and distance tracking for all registered agents
        foreach (var pair in agentLapData)
        {
            var data = pair.Value;
            CarAgent agent = null;
            
            // Find the agent in the scene based on its ID
            foreach (var carAgent in FindObjectsByType<CarAgent>(FindObjectsSortMode.None))
            {
                if (carAgent.gameObject.GetInstanceID().ToString() == data.agentId)
                {
                    agent = carAgent;
                    break;
                }
            }
            
            if (agent != null && agent.gameObject.activeSelf)
            {
                // Track distance for this lap
                float distanceTraveled = Vector3.Distance(agent.transform.position, data.lastPosition);
                data.totalLapDistance += distanceTraveled;
                data.lastPosition = agent.transform.position;
            }
        }
    }
    
    public void CheckpointPassed(CarAgent agent, int checkpointIndex)
    {
        string agentId = agent.gameObject.GetInstanceID().ToString();
        
        if (!agentLapData.ContainsKey(agentId))
        {
            RegisterAgent(agent);
        }
        
        var data = agentLapData[agentId];
        
        // Add this checkpoint to visited set
        data.visitedCheckpoints.Add(checkpointIndex);
        
        // Check if agent has visited enough checkpoints for lap completion
        if (trackGenerator != null)
        {
            int totalCheckpoints = trackGenerator.GetCheckpoints().Count;
            int requiredCheckpoints = Mathf.CeilToInt(totalCheckpoints * requiredCheckpointCompletion);
            
            // Update eligibility based on percentage completion
            if (data.visitedCheckpoints.Count >= requiredCheckpoints)
            {
                data.eligibleForLapCompletion = true;
                
                if (showDebugMessages)
                {
                    Debug.Log($"Agent {agent.name} has visited {data.visitedCheckpoints.Count}/{totalCheckpoints} checkpoints " +
                              $"({data.visitedCheckpoints.Count * 100f / totalCheckpoints:F1}%) and is eligible for lap completion");
                }
            }
        }
    }
    
    public void StartFinishLineCrossed(CarAgent agent)
    {
        string agentId = agent.gameObject.GetInstanceID().ToString();
        
        if (!agentLapData.ContainsKey(agentId))
        {
            RegisterAgent(agent);
            return; // First crossing is just starting the lap
        }
        
        var data = agentLapData[agentId];
        
        // Check if this is a lap start (first crossing or after completion)
        if (data.currentLap == 0 || data.visitedCheckpoints.Count == 0)
        {
            // Starting a new lap
            data.lapStartTime = Time.time;
            data.visitedCheckpoints.Clear();
            data.visitedCheckpoints.Add(0); // Add start/finish checkpoint
            data.eligibleForLapCompletion = false;
            data.totalLapDistance = 0f;
            
            if (showDebugMessages)
            {
                Debug.Log($"Agent {agent.name} started lap {data.currentLap + 1}");
            }
            
            return;
        }
        
        // Check if the agent is eligible for lap completion
        if (data.eligibleForLapCompletion)
        {
            // Complete the lap
            data.currentLap++;
            float lapTime = Time.time - data.lapStartTime;
            
            // Check if this is a new best lap
            if (lapTime < data.bestLapTime)
            {
                data.bestLapTime = lapTime;
            }
            
            // Calculate lap metrics
            float avgSpeed = data.totalLapDistance / lapTime;
            
            // Record lap time
            LapTimeRecord record = new LapTimeRecord
            {
                lapNumber = data.currentLap,
                lapTime = lapTime,
                agentName = agent.name
            };
            
            allLapTimeRecords.Add(record);
            
            if (showDebugMessages)
            {
                Debug.Log($"Agent {agent.name} completed lap {data.currentLap} in {lapTime:F2} seconds " +
                          $"(Avg Speed: {avgSpeed:F2} m/s, Distance: {data.totalLapDistance:F1}m)");
            }
            
            // Reset for next lap
            data.lapStartTime = Time.time;
            data.visitedCheckpoints.Clear();
            data.visitedCheckpoints.Add(0); // Add start/finish checkpoint
            data.eligibleForLapCompletion = false;
            data.totalLapDistance = 0f;
            
            // Notify the agent of lap completion (for rewards)
            agent.OnLapCompleted(data.currentLap, lapTime);
        }
        else
        {
            // The agent crossed the start/finish line but hasn't visited enough checkpoints
            if (trackGenerator != null)
            {
                int totalCheckpoints = trackGenerator.GetCheckpoints().Count;
                
                if (showDebugMessages)
                {
                    Debug.Log($"Agent {agent.name} crossed start/finish but only visited {data.visitedCheckpoints.Count}/{totalCheckpoints} " +
                              $"checkpoints ({data.visitedCheckpoints.Count * 100f / totalCheckpoints:F1}%), " +
                              $"needs {requiredCheckpointCompletion * 100f:F0}% to complete lap");
                }
            }
            else
            {
                if (showDebugMessages)
                {
                    Debug.Log($"Agent {agent.name} crossed start/finish but hasn't visited enough checkpoints yet");
                }
            }
        }
    }
    
    // Call these methods from the agent's checkpoint trigger events
    
    public int GetCurrentLap(CarAgent agent)
    {
        string agentId = agent.gameObject.GetInstanceID().ToString();
        return agentLapData.ContainsKey(agentId) ? agentLapData[agentId].currentLap : 0;
    }
    
    public float GetBestLapTime(CarAgent agent)
    {
        string agentId = agent.gameObject.GetInstanceID().ToString();
        return agentLapData.ContainsKey(agentId) ? agentLapData[agentId].bestLapTime : float.MaxValue;
    }
    
    public float GetCurrentLapTime(CarAgent agent)
    {
        string agentId = agent.gameObject.GetInstanceID().ToString();
        if (agentLapData.ContainsKey(agentId))
        {
            return Time.time - agentLapData[agentId].lapStartTime;
        }
        return 0;
    }
    
    public LapTimeRecord[] GetAllLapRecords()
    {
        return allLapTimeRecords.ToArray();
    }
    
    public LapTimeRecord[] GetBestLapRecords(int count = 5)
    {
        return allLapTimeRecords
            .OrderBy(record => record.lapTime)
            .Take(count)
            .ToArray();
    }
    
    public LapTimeRecord[] GetAgentLapRecords(CarAgent agent)
    {
        return allLapTimeRecords
            .Where(record => record.agentName == agent.name)
            .OrderBy(record => record.lapNumber)
            .ToArray();
    }
    
    // Check if an agent has visited a specific checkpoint
    public bool HasVisitedCheckpoint(CarAgent agent, int checkpointIndex)
    {
        string agentId = agent.gameObject.GetInstanceID().ToString();
        if (agentLapData.ContainsKey(agentId))
        {
            return agentLapData[agentId].visitedCheckpoints.Contains(checkpointIndex);
        }
        return false;
    }
    
    // Get percentage of track completed (based on checkpoints)
    public float GetLapCompletion(CarAgent agent)
    {
        string agentId = agent.gameObject.GetInstanceID().ToString();
        if (agentLapData.ContainsKey(agentId) && trackGenerator != null)
        {
            int totalCheckpoints = trackGenerator.GetCheckpoints().Count;
            if (totalCheckpoints > 0)
            {
                return (float)agentLapData[agentId].visitedCheckpoints.Count / totalCheckpoints;
            }
        }
        return 0;
    }
    
    // Reset lap data for an agent (e.g., when restarting an episode)
    public void ResetLapData(CarAgent agent)
    {
        string agentId = agent.gameObject.GetInstanceID().ToString();
        if (agentLapData.ContainsKey(agentId))
        {
            var data = agentLapData[agentId];
            data.lapStartTime = Time.time;
            data.visitedCheckpoints.Clear();
            data.eligibleForLapCompletion = false;
            data.totalLapDistance = 0f;
            data.lastPosition = agent.transform.position;
            // Note: We don't reset currentLap or bestLapTime here as they're cumulative stats
        }
        else
        {
            // If the agent isn't registered yet, register it
            RegisterAgent(agent);
        }
    }
}