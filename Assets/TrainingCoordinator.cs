using UnityEngine;
using Unity.MLAgents;
using System.Collections;

public class TrainingCoordinator : MonoBehaviour
{
    public GeneticPPOManager geneticManager;
    public PPOGATrainer ppoTrainer;
    public SplineTrackGenerator trackGenerator;
    public bool generateRandomTracksEachGeneration = true;
    public int maxGenerations = 50;
    public float trainingTimeScale = 5.0f;
    
    // Monitoring
    private int totalEpisodesRun = 0;
    private float bestFitness = 0;
    private float bestLapTime = float.MaxValue;
    private int bestLapCount = 0;
    public bool simultaneousTraining = true;
    public int maxSimultaneousCars = 5;

    // UI Output (you could connect this to UI elements)
    public string statusText = "Initializing...";
    public void SetupSimultaneousTraining()
{
    if (geneticManager == null) return;
    
    // Configure genetic manager for simultaneous training
    geneticManager.useSimultaneousTraining = simultaneousTraining;
    geneticManager.maxSimultaneousCars = maxSimultaneousCars;
    
    // Determine spacing between cars
    float trackLength = 0;
    if (trackGenerator != null)
    {
        // Estimate track length based on number of checkpoints and spacing
        var checkpoints = trackGenerator.GetCheckpoints();
        if (checkpoints != null && checkpoints.Count > 0)
        {
            // Rough estimation of track length
            trackLength = checkpoints.Count * 20f; // Assuming average 20m between checkpoints
            
            // Distribute cars evenly along track
            geneticManager.carSpacing = trackLength / Mathf.Max(2, maxSimultaneousCars);
            
            Debug.Log($"Estimated track length: {trackLength}m, car spacing: {geneticManager.carSpacing}m");
        }
    }
}

    void Start()
{
    // Make sure ML-Agents Academy is initialized
    if (Academy.IsInitialized)
    {
        Debug.Log("ML-Agents Academy is initialized");
    }
    else
    {
        Debug.LogError("ML-Agents Academy is not initialized!");
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
    
    // Set up simultaneous training
    SetupSimultaneousTraining();
    
    // Connect components
    geneticManager.ppoTrainer = ppoTrainer;
    ppoTrainer.geneticManager = geneticManager;
    
    // Generate initial track if needed
    if (trackGenerator != null && generateRandomTracksEachGeneration)
    {
        trackGenerator.GenerateTrack();
    }
    
    StartCoroutine(MonitorTraining());
}
    
    IEnumerator MonitorTraining()
    {
        while (geneticManager.currentGeneration <= maxGenerations)
        {
            // Update stats
            UpdateTrainingStats();
            
            // Generate new track for next generation if enabled
            if (generateRandomTracksEachGeneration && 
                geneticManager.currentEpisode == geneticManager.geneticParams.generationEpisodes)
            {
                // Prepare to generate a new track for next generation
                yield return new WaitForEndOfFrame();
                RandomizeTrack();
            }
            
            yield return new WaitForSeconds(1.0f); // Check every second
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
                            geneticManager.activeGenomeIndex;
        
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
        
        // Update status text
        statusText = $"Generation: {geneticManager.currentGeneration}/{maxGenerations}\n" +
                     $"Episode: {geneticManager.currentEpisode}/{geneticManager.geneticParams.generationEpisodes}\n" +
                     $"Best Fitness: {bestFitness:F2}\n" +
                     $"Best Lap Count: {bestLapCount}\n" +
                     $"Episodes Run: {totalEpisodesRun}";
        
        Debug.Log(statusText);
    }
    
    void RandomizeTrack()
    {
        if (trackGenerator == null) return;
        
        // Restart track generation to create variation
        trackGenerator.GenerateTrack();
        
        Debug.Log("Generated new track for next generation");
    }
    
    // Method to manually trigger episode completion (can be called by the agent)
    public void NotifyEpisodeComplete()
    {
        if (geneticManager != null)
        {
            geneticManager.OnEpisodeComplete();
        }
    }
    
    // For manually saving the best model
    public void SaveBestModel()
    {
        if (geneticManager.population.Count == 0 || ppoTrainer == null) return;
        
        // Find the best genome
        CarGenome bestGenome = null;
        float highestFitness = float.MinValue;
        
        foreach (var genome in geneticManager.population)
        {
            if (genome.fitness > highestFitness)
            {
                highestFitness = genome.fitness;
                bestGenome = genome;
            }
        }
        
        if (bestGenome != null)
        {
            ppoTrainer.SaveModel(bestGenome, geneticManager.currentGeneration);
        }
    }
}