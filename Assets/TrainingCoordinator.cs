using UnityEngine;
using Unity.MLAgents;
using System.Collections;
using System.IO;
using System.Linq;

public class TrainingCoordinator : MonoBehaviour
{
    public GeneticPPOManager geneticManager;
    public PPOGATrainer ppoTrainer;
    public SplineTrackGenerator trackGenerator;
    
    [Header("Training Settings")]
    public int maxGenerations = 100;
    public float trainingTimeScale = 2.0f;
    public bool loadFromSavedProgress = false;
    
    [Header("Multi-Car Training")]
    public bool enableMultiCarTraining = true;
    public int carsPerGeneration = 5;
    
    // Visualization and monitoring
    private int totalEpisodesRun = 0;
    private float bestFitness = 0;
    private int bestLapCount = 0;
    
    // Training Phases
    [Header("Training Phases")]
    [Range(1, 3)]
    public int currentPhase = 1;
    
    // UI Output
    public string statusText = "Initializing...";
    
    void Start()
    {
        // Make sure ML-Agents Academy is initialized
        if (Academy.IsInitialized)
        {
            Debug.Log("ML-Agents Academy is initialized");
        }
        else
        {
            Debug.LogWarning("ML-Agents Academy is not initialized. Running in inference mode only.");
        }
        
        // Set time scale for faster training
        Time.timeScale = trainingTimeScale;
        
        // Check for required components
        if (geneticManager == null)
        {
            Debug.LogError("GeneticPPOManager is not assigned!");
            return;
        }
        
        if (ppoTrainer == null)
        {
            Debug.LogError("PPOGATrainer is not assigned!");
            return;
        }
        
        // Configure for multi-car training
        if (enableMultiCarTraining)
        {
            geneticManager.useSimultaneousTraining = true;
            geneticManager.maxSimultaneousCars = carsPerGeneration;
        }
        else
        {
            geneticManager.useSimultaneousTraining = false;
        }
        
        // Connect components
        geneticManager.ppoTrainer = ppoTrainer;
        ppoTrainer.geneticManager = geneticManager;
        
        if (loadFromSavedProgress)
        {
            LoadTrainingProgress();
        }
        
        // Generate initial track if needed
        if (trackGenerator != null)
        {
            if (trackGenerator.GetCheckpoints().Count == 0)
            {
                trackGenerator.GenerateTrack();
            }
        }
        
        StartCoroutine(MonitorTraining());
    }
    
    IEnumerator MonitorTraining()
    {
        while (geneticManager.currentGeneration <= maxGenerations)
        {
            // Update stats
            UpdateTrainingStats();
            
            yield return new WaitForSeconds(5.0f); // Check every 5 seconds
        }
        
        // Training complete
        Debug.Log("Training complete!");
        statusText = "Training complete!";
    }
    
    void UpdateTrainingStats()
    {
        if (geneticManager.population.Count == 0) return;
        
        // Update total episodes
        totalEpisodesRun = (geneticManager.currentGeneration - 1) * 
                            geneticManager.geneticParams.populationSize * 
                            geneticManager.geneticParams.generationEpisodes + 
                            (geneticManager.currentEpisode - 1) * geneticManager.geneticParams.populationSize +
                            geneticManager.activeGenomeIndices.Count;
        
        // Find best fitness in current population
        float currentBestFitness = 0;
        int currentBestLaps = 0;
        
        foreach (var genome in geneticManager.population)
        {
            if (genome.fitness > currentBestFitness)
            {
                currentBestFitness = genome.fitness;
            }
            
            if (genome.lapsCompleted > currentBestLaps)
            {
                currentBestLaps = genome.lapsCompleted;
            }
        }
        
        // Update all-time best
        if (currentBestFitness > bestFitness)
        {
            bestFitness = currentBestFitness;
        }
        
        if (currentBestLaps > bestLapCount)
        {
            bestLapCount = currentBestLaps;
        }
        
        // Calculate completion rates for phase tracking
        float checkpointCompletionRate = 0;
        float lapCompletionRate = 0;
        
        if (geneticManager.population.Count > 0)
        {
            int checkpointThreshold = 5; // Consider "completed" if at least 5 checkpoints passed
            int checkpointPassedCount = geneticManager.population.Count(g => g.checkpointsPassed >= checkpointThreshold);
            int lapCompletedCount = geneticManager.population.Count(g => g.lapsCompleted > 0);
            
            checkpointCompletionRate = (float)checkpointPassedCount / geneticManager.population.Count;
            lapCompletionRate = (float)lapCompletedCount / geneticManager.population.Count;
        }
        
        // Determine current phase
        currentPhase = DetermineCurrentPhase(checkpointCompletionRate, lapCompletionRate);
        
        // Update status text
        statusText = $"Generation: {geneticManager.currentGeneration}/{maxGenerations}\n" +
                     $"Episode: {geneticManager.currentEpisode}/{geneticManager.geneticParams.generationEpisodes}\n" +
                     $"Phase: {currentPhase}\n" +
                     $"Best Fitness: {bestFitness:F2}\n" +
                     $"Best Lap Count: {bestLapCount}\n" +
                     $"Checkpoint Completion: {checkpointCompletionRate:P1}\n" +
                     $"Lap Completion: {lapCompletionRate:P1}\n" +
                     $"Episodes Run: {totalEpisodesRun}";
        
        Debug.Log(statusText);
    }
    
    private int DetermineCurrentPhase(float checkpointRate, float lapRate)
    {
        if (lapRate >= 0.3f)
        {
            return 3; // Time optimization
        }
        else if (checkpointRate >= 0.7f)
        {
            return 2; // Speed optimization
        }
        else
        {
            return 1; // Navigation
        }
    }
    
    // Method to manually trigger episode completion (can be called by the agent)
    public void NotifyEpisodeComplete()
    {
        if (geneticManager != null)
        {
            geneticManager.OnEpisodeComplete();
        }
    }
    
    // Method to save current training state
    private void SaveTrainingProgress()
    {
        // Save generation metrics
        string saveDir = "Assets/TrainingData";
        
        if (!Directory.Exists(saveDir))
        {
            Directory.CreateDirectory(saveDir);
        }
        
        // Save basic metrics
        string metricsPath = Path.Combine(saveDir, "training_state.json");
        TrainingState state = new TrainingState
        {
            currentGeneration = geneticManager.currentGeneration,
            totalEpisodes = totalEpisodesRun,
            bestFitness = bestFitness,
            bestLapCount = bestLapCount,
            currentPhase = currentPhase
        };
        
        string json = JsonUtility.ToJson(state, true);
        File.WriteAllText(metricsPath, json);
        
        Debug.Log($"Training progress saved to {metricsPath}");
    }
    
    // Method to load training state
    private void LoadTrainingProgress()
    {
        string statePath = "Assets/TrainingData/training_state.json";
        
        if (File.Exists(statePath))
        {
            try
            {
                string json = File.ReadAllText(statePath);
                TrainingState state = JsonUtility.FromJson<TrainingState>(json);
                
                // Restore metrics
                bestFitness = state.bestFitness;
                bestLapCount = state.bestLapCount;
                currentPhase = state.currentPhase;
                
                Debug.Log($"Loaded training state: Generation {state.currentGeneration}, Phase {state.currentPhase}");
                
                // Also try to load model parameters
                if (ppoTrainer != null)
                {
                    PPOHyperparameters parameters = ppoTrainer.LoadLatestModel();
                    if (parameters != null && geneticManager.defaultPPOParams != null)
                    {
                        geneticManager.defaultPPOParams = parameters;
                        Debug.Log("Loaded model parameters from saved state");
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogError($"Error loading training state: {e.Message}");
            }
        }
        else
        {
            Debug.LogWarning("No saved training state found. Starting from scratch.");
        }
    }
    
    // Class to store training state
    [System.Serializable]
    private class TrainingState
    {
        public int currentGeneration;
        public int totalEpisodes;
        public float bestFitness;
        public int bestLapCount;
        public int currentPhase;
    }
}