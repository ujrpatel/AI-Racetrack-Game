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
                lapStartTime = Time.time
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
        
        // Check if agent has visited all checkpoints
        if (trackGenerator != null)
        {
            int totalCheckpoints = trackGenerator.GetCheckpoints().Count;
            
            // Agent must visit all checkpoints except start/finish to be eligible for lap completion
            if (data.visitedCheckpoints.Count >= totalCheckpoints - 1)
            {
                // Make sure all checkpoints are visited (except possibly the start/finish which has index 0)
                bool allVisited = true;
                for (int i = 1; i < totalCheckpoints; i++)
                {
                    if (!data.visitedCheckpoints.Contains(i))
                    {
                        allVisited = false;
                        break;
                    }
                }
                
                if (allVisited)
                {
                    data.eligibleForLapCompletion = true;
                    
                    if (showDebugMessages)
                    {
                        Debug.Log($"Agent {agent.name} has visited all checkpoints and is eligible for lap completion");
                    }
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
                Debug.Log($"Agent {agent.name} completed lap {data.currentLap} in {lapTime:F2} seconds");
            }
            
            // Reset for next lap
            data.lapStartTime = Time.time;
            data.visitedCheckpoints.Clear();
            data.visitedCheckpoints.Add(0); // Add start/finish checkpoint
            data.eligibleForLapCompletion = false;
            
            // Notify the agent of lap completion (for rewards)
            agent.OnLapCompleted(data.currentLap, lapTime);
        }
        else
        {
            if (showDebugMessages)
            {
                // The agent crossed the start/finish line but hasn't visited all checkpoints
                Debug.Log($"Agent {agent.name} crossed start/finish but hasn't visited all checkpoints yet");
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
            // Note: We don't reset currentLap or bestLapTime here as they're cumulative stats
        }
    }
}