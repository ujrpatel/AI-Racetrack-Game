using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class PPOGATrainer : MonoBehaviour
{
    [System.Serializable]
    public class TrainingConfiguration
    {
        // Current active configuration
        public PPOHyperparameters ppoParams = new PPOHyperparameters();
        
        // Reward function parameters that can be evolved
        public float speedRewardFactor = 0.01f;
        public float alignmentRewardFactor = 0.02f;
        public float checkpointReward = 1.0f;
        public float lapCompletionReward = 5.0f;
        public float collisionPenalty = 1.0f;
        public float timeoutPenalty = 0.5f;
    }
    
    // Training configuration to use
    public TrainingConfiguration trainingConfig;
    
    // Agent references
    private List<CarAgent> activeAgents = new List<CarAgent>();
    
    // Link to genetic manager
    public GeneticPPOManager geneticManager;
    
    // Training metrics
    private float episodeReward = 0;
    private int episodeSteps = 0;
    private int totalSteps = 0;
    
    // Model saving
    public string modelSavePath = "Assets/Models/";
    public int saveModelInterval = 10; // Save every N generations
    
    void Start()
{
    // Create models directory if it doesn't exist
    if (!string.IsNullOrEmpty(modelSavePath) && !Directory.Exists(modelSavePath))
    {
        try
        {
            Directory.CreateDirectory(modelSavePath);
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"Could not create models directory: {e.Message}");
        }
    }
    
    // Subscribe to ML-Agents events (with safety check)
    try
    {
        if (Academy.IsInitialized)
        {
            Academy.Instance.AgentPreStep += OnAgentPreStep;
            Debug.Log("Registered with ML-Agents Academy");
        }
        else
        {
            Debug.LogWarning("ML-Agents Academy is not initialized, some functionality may be limited");
        }
    }
    catch (System.Exception e)
    {
        Debug.LogError($"Error initializing PPOGATrainer: {e.Message}");
    }
}
    
    void OnDestroy()
    {
        if (Academy.IsInitialized)
        {
            Academy.Instance.AgentPreStep -= OnAgentPreStep;
        }
    }
    
    public void RegisterAgent(CarAgent agent)
    {
        if (!activeAgents.Contains(agent))
        {
            activeAgents.Add(agent);
            
            // Apply current training configuration to agent
            ApplyTrainingConfigToAgent(agent);
        }
    }
    
    public void UnregisterAgent(CarAgent agent)
    {
        if (activeAgents.Contains(agent))
        {
            activeAgents.Remove(agent);
        }
    }
    
    void OnAgentPreStep(int stepCount)
    {
        // Update tracking metrics
        totalSteps++;
        episodeSteps++;
    }
    
    public void ApplyTrainingConfigToAgent(CarAgent agent)
    {
        if (agent == null) return;
        
        // Set reward function parameters
        agent.UpdateRewardParameters(
            trainingConfig.speedRewardFactor,
            trainingConfig.checkpointReward,
            trainingConfig.lapCompletionReward,
            trainingConfig.collisionPenalty
        );
        
        // Apply PPO parameters - this is a placeholder since we can't directly access the
        // ML-Agents internal PPO implementation in code
        // The actual parameter adjustment would happen via ML-Agents config
        BehaviorParameters behaviorParams = agent.GetComponent<BehaviorParameters>();
        if (behaviorParams != null)
        {
            // Log that we're applying parameters
            Debug.Log($"Applied PPO params to agent: LR={trainingConfig.ppoParams.learningRate}, Gamma={trainingConfig.ppoParams.gamma}");
        }
    }
    
    public void OnEpisodeEnd(CarAgent agent, float accumulatedReward)
    {
        // Update metrics
        episodeReward = accumulatedReward;
        
        // Log performance
        Debug.Log($"Episode ended. Steps: {episodeSteps}, Reward: {episodeReward}");
        
        // Reset counters
        episodeSteps = 0;
    }
    
    public void ApplyGenomeToConfig(CarGenome genome)
    {
        if (genome == null) return;
        
        // Apply PPO parameters from genome
        trainingConfig.ppoParams = genome.policyParams.Clone();
        
        // Update all active agents
        foreach (var agent in activeAgents)
        {
            ApplyTrainingConfigToAgent(agent);
        }
    }
    
    public void OnGenerationEnd(int generation)
    {
        // Save model periodically
        if (generation % saveModelInterval == 0 && geneticManager != null)
        {
            // Find best genome
            CarGenome bestGenome = geneticManager.population
                .OrderByDescending(g => g.fitness)
                .FirstOrDefault();
                
            if (bestGenome != null)
            {
                SaveModel(bestGenome, generation);
            }
        }
    }
    
    public void SaveModel(CarGenome bestGenome, int generation)
    {
        // Create model filename
        string filename = $"car_model_gen{generation}_fit{bestGenome.fitness:F2}.txt";
        string filePath = Path.Combine(modelSavePath, filename);
        
        // Save hyperparameters to file
        string hyperparamsJson = JsonUtility.ToJson(bestGenome.policyParams, true);
        File.WriteAllText(filePath, hyperparamsJson);
        
        // Note: In a real implementation, you would save the actual neural network model
        // ML-Agents has built-in model saving functionality, but we can't access it directly here
        
        Debug.Log($"Saved model parameters to {filePath}");
    }
}