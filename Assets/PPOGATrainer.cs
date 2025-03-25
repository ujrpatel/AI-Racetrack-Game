using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Collections;

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
    private object agentLock = new object(); // For thread safety
    
    // Link to genetic manager
    public GeneticPPOManager geneticManager;
    
    // Training metrics
    private float episodeReward = 0;
    private int episodeSteps = 0;
    private int totalSteps = 0;
    private int trainingPhase = 1; // Track current training phase
    
    // Model saving
    public string modelSavePath = "Assets/Models/";
    public int saveModelInterval = 5; // Save every N generations
    
    // Training phases
    private float checkpointCompletionThreshold = 0.7f; // 70% of cars must reach checkpoints to progress
    private float lapCompletionThreshold = 0.3f; // 30% of cars must complete laps to progress
    
    void Start()
    {
        // Create models directory if it doesn't exist
        if (!Directory.Exists(modelSavePath))
        {
            Directory.CreateDirectory(modelSavePath);
        }
        
        // Ensure ML-Agents Academy is initialized properly
        StartCoroutine(EnsureAcademyInitialization());
    }
    
    private IEnumerator EnsureAcademyInitialization()
    {
        // Wait a short moment for other systems to initialize
        yield return new WaitForSeconds(0.1f);
        
        if (!Academy.IsInitialized)
        {
            Debug.LogWarning("ML-Agents Academy not initialized. This might impact training functionality.");
            
            // Try to force initialization by referencing the Instance
            try 
            {
                var _ = Academy.Instance;
                Debug.Log("Successfully initialized ML-Agents Academy");
                Academy.Instance.AgentPreStep += OnAgentPreStep;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Failed to initialize ML-Agents Academy: {e.Message}");
                Debug.LogWarning("Training will continue in inference mode - only model execution, no learning");
            }
        }
        else
        {
            Debug.Log("ML-Agents Academy is already initialized");
            Academy.Instance.AgentPreStep += OnAgentPreStep;
        }
        
        // Continue with the rest of initialization regardless of Academy status
        StartCoroutine(PeriodicPhaseCheck());
        
        // If we have a genetic manager, make sure it's properly linked
        if (geneticManager != null)
        {
            geneticManager.ppoTrainer = this;
            Debug.Log("Successfully linked GeneticPPOManager with PPOGATrainer");
        }
        else
        {
            Debug.LogError("GeneticPPOManager reference is missing. Please assign it in the inspector.");
        }
    }
    
    IEnumerator PeriodicPhaseCheck()
    {
        // Wait for initial training to begin
        yield return new WaitForSeconds(60f);
        
        while (true)
        {
            // Check training progression every 2 minutes
            CheckTrainingPhase();
            yield return new WaitForSeconds(120f);
        }
    }
    
    private void CheckTrainingPhase()
    {
        if (geneticManager == null || geneticManager.population.Count == 0) return;
        
        var population = geneticManager.population;
        
        // Check checkpoint completion rate
        float checkpointCompletionRate = (float)population.Count(g => g.checkpointsPassed >= 5) / population.Count;
        
        // Check lap completion rate
        float lapCompletionRate = (float)population.Count(g => g.lapsCompleted > 0) / population.Count;
        
        Debug.Log($"Training metrics - Checkpoint completion: {checkpointCompletionRate:P2}, Lap completion: {lapCompletionRate:P2}, Current phase: {trainingPhase}");
        
        // Phase progression logic
        if (trainingPhase == 1 && checkpointCompletionRate >= checkpointCompletionThreshold)
        {
            trainingPhase = 2;
            UpdatePhaseConfiguration();
            Debug.Log("Training progressed to Phase 2: Speed Optimization");
        }
        else if (trainingPhase == 2 && lapCompletionRate >= lapCompletionThreshold)
        {
            trainingPhase = 3;
            UpdatePhaseConfiguration();
            Debug.Log("Training progressed to Phase 3: Time Optimization");
        }
    }
    
    private void UpdatePhaseConfiguration()
    {
        switch (trainingPhase)
        {
            case 1: // Navigation phase
                trainingConfig.speedRewardFactor = 0.01f;
                trainingConfig.checkpointReward = 1.0f;
                trainingConfig.lapCompletionReward = 5.0f;
                break;
                
            case 2: // Speed optimization
                trainingConfig.speedRewardFactor = 0.02f;
                trainingConfig.checkpointReward = 0.5f;
                trainingConfig.lapCompletionReward = 3.0f;
                break;
                
            case 3: // Time optimization
                trainingConfig.speedRewardFactor = 0.03f;
                trainingConfig.checkpointReward = 0.2f;
                trainingConfig.lapCompletionReward = 2.0f;
                break;
        }
        
        // Apply updated configuration to all active agents
        lock (agentLock)
        {
            // Create a copy of the list to avoid potential modification during iteration
            List<CarAgent> agentsCopy = new List<CarAgent>(activeAgents);
            
            foreach (var agent in agentsCopy)
            {
                if (agent != null && agent.gameObject != null && agent.isActiveAndEnabled)
                {
                    ApplyTrainingConfigToAgent(agent);
                }
            }
        }
    }
    
    void OnDestroy()
    {
        // Cleanup event subscriptions
        if (Academy.IsInitialized)
        {
            Academy.Instance.AgentPreStep -= OnAgentPreStep;
        }
    }
    
    public void RegisterAgent(CarAgent agent)
    {
        if (agent == null) return;
        
        lock (agentLock)
        {
            // Remove any null references first
            activeAgents.RemoveAll(a => a == null);
            
            if (!activeAgents.Contains(agent))
            {
                activeAgents.Add(agent);
                
                // Apply current training configuration to agent
                ApplyTrainingConfigToAgent(agent);
                
                Debug.Log($"Registered agent {agent.name} with trainer");
            }
        }
    }
    
    public void UnregisterAgent(CarAgent agent)
    {
        if (agent == null) return;
        
        lock (agentLock)
        {
            if (activeAgents.Contains(agent))
            {
                activeAgents.Remove(agent);
                Debug.Log($"Unregistered agent {agent.name} from trainer");
            }
            
            // Also clean up any null references
            activeAgents.RemoveAll(a => a == null);
        }
    }
    
    void OnAgentPreStep(int stepCount)
    {
        // Update tracking metrics in a thread-safe way
        lock (agentLock)
        {
            totalSteps++;
            episodeSteps++;
        }
    }
    
    public void ApplyTrainingConfigToAgent(CarAgent agent)
    {
        if (agent == null) return;
        
        try
        {
            // Ensure the agent is still valid before applying config
            if (agent.gameObject != null && agent.isActiveAndEnabled)
            {
                // Set reward function parameters based on training phase
                agent.UpdateRewardParameters(
                    trainingConfig.speedRewardFactor,
                    trainingConfig.checkpointReward,
                    trainingConfig.lapCompletionReward,
                    trainingConfig.collisionPenalty
                );
                
                // Apply exploration settings based on phase
                switch (trainingPhase)
                {
                    case 1:
                        agent.forceExploration = Random.value < 0.3f; // 30% exploration in phase 1
                        break;
                    case 2:
                        agent.forceExploration = Random.value < 0.15f; // 15% exploration in phase 2
                        break;
                    case 3:
                        agent.forceExploration = Random.value < 0.05f; // 5% exploration in phase 3
                        break;
                }
                
                // Debug log on configuration
                BehaviorParameters behaviorParams = agent.GetComponent<BehaviorParameters>();
                if (behaviorParams != null && trainingConfig.ppoParams != null)
                {
                    if (agent.enableDebugLogging)
                    {
                        Debug.Log($"Applied PPO params to agent {agent.name}: " +
                             $"LR={trainingConfig.ppoParams.learningRate}, " +
                             $"Gamma={trainingConfig.ppoParams.gamma}, " +
                             $"Phase={trainingPhase}");
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error applying training config to agent: {e.Message}");
            
            // Remove this agent if it's causing errors
            lock (agentLock)
            {
                activeAgents.Remove(agent);
            }
        }
    }
    
    public void OnEpisodeEnd(CarAgent agent, float accumulatedReward)
    {
        if (agent == null) return;
        
        // Update metrics
        lock (agentLock)
        {
            episodeReward = accumulatedReward;
            
            // Log performance
            Debug.Log($"Episode ended for {agent.name}. Steps: {episodeSteps}, Reward: {episodeReward:F2}");
            
            // Reset counters
            episodeSteps = 0;
            
            // Unregister this agent
            activeAgents.Remove(agent);
        }
    }
    
    public void ApplyGenomeToConfig(CarGenome genome)
    {
        if (genome == null || genome.policyParams == null) return;
        
        try
        {
            // Apply PPO parameters from genome
            trainingConfig.ppoParams = genome.policyParams.Clone();
            
            // Update agent if it's active
            if (genome.agent != null && genome.agent.gameObject != null && genome.agent.isActiveAndEnabled)
            {
                lock (agentLock)
                {
                    if (activeAgents.Contains(genome.agent))
                    {
                        ApplyTrainingConfigToAgent(genome.agent);
                    }
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error applying genome to config: {e.Message}");
        }
    }
    
    public void OnGenerationEnd(int generation)
    {
        try
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
            
            // Clean up agent references at the end of generation
            lock (agentLock)
            {
                activeAgents.RemoveAll(a => a == null || !a.isActiveAndEnabled);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error in OnGenerationEnd: {e.Message}");
        }
    }
    
    public void SaveModel(CarGenome bestGenome, int generation)
    {
        // Create model filename with metrics
        string filename = $"car_model_gen{generation}_fit{bestGenome.fitness:F2}_laps{bestGenome.lapsCompleted}.json";
        string filePath = Path.Combine(modelSavePath, filename);
        
        // Save hyperparameters to file
        string hyperparamsJson = JsonUtility.ToJson(bestGenome.policyParams, true);
        File.WriteAllText(filePath, hyperparamsJson);
        
        // Also save a version marked as 'latest' for easy loading
        string latestPath = Path.Combine(modelSavePath, "latest_model.json");
        File.WriteAllText(latestPath, hyperparamsJson);
        
        Debug.Log($"Saved model parameters to {filePath}");
    }
    
    // Method to load a saved model
    public PPOHyperparameters LoadLatestModel()
    {
        string latestPath = Path.Combine(modelSavePath, "latest_model.json");
        
        if (File.Exists(latestPath))
        {
            try
            {
                string json = File.ReadAllText(latestPath);
                PPOHyperparameters parameters = JsonUtility.FromJson<PPOHyperparameters>(json);
                Debug.Log("Loaded latest model parameters");
                return parameters;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error loading model: {e.Message}");
            }
        }
        else
        {
            Debug.LogWarning("No saved model found, using default parameters");
        }
        
        // Return default parameters if loading fails
        return new PPOHyperparameters();
    }
}