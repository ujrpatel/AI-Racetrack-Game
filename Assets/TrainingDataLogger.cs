using UnityEngine;
using System.IO;
using System.Collections.Generic;
using System.Linq;

public class TrainingDataLogger : MonoBehaviour
{
    public GeneticPPOManager geneticManager;
    public TrainingCoordinator coordinator;
    
    [Header("Logging Settings")]
    public float loggingInterval = 60f; // Log every minute
    public string logDirectory = "Assets/TrainingData/Logs";
    public bool saveDetailedCarData = true;
    
    // Tracking data
    private List<TrainingMetric> trainingHistory = new List<TrainingMetric>();
    private float nextLogTime;
    
    [System.Serializable]
    public class TrainingMetric
    {
        public int generation;
        public int episode;
        public float timestamp;
        public float avgFitness;
        public float maxFitness;
        public float avgCheckpoints;
        public float maxCheckpoints;
        public int totalLaps;
        public float avgSpeed;
        public int trainingPhase;
    }
    
    void Start()
    {
        // Create log directory if it doesn't exist
        if (!Directory.Exists(logDirectory))
        {
            Directory.CreateDirectory(logDirectory);
        }
        
        // Initialize logging time
        nextLogTime = Time.time + loggingInterval;
    }
    
    void Update()
    {
        // Check if it's time to log data
        if (Time.time >= nextLogTime && geneticManager != null && geneticManager.population.Count > 0)
        {
            LogTrainingData();
            nextLogTime = Time.time + loggingInterval;
        }
    }
    
    private void LogTrainingData()
    {
        // Skip if no population
        if (geneticManager.population.Count == 0)
            return;
            
        // Calculate metrics
        float avgFitness = (float)geneticManager.population.Average(g => g.fitness);
        float maxFitness = (float)geneticManager.population.Max(g => g.fitness);
        float avgCheckpoints = (float)geneticManager.population.Average(g => g.checkpointsPassed);
        float maxCheckpoints = (float)geneticManager.population.Max(g => g.checkpointsPassed);
        int totalLaps = geneticManager.population.Sum(g => g.lapsCompleted);
        float avgSpeed = (float)geneticManager.population.Average(g => g.avgSpeed);
        int phase = coordinator != null ? coordinator.currentPhase : 1;
        
        // Create metric record
        TrainingMetric metric = new TrainingMetric
        {
            generation = geneticManager.currentGeneration,
            episode = geneticManager.currentEpisode,
            timestamp = Time.time,
            avgFitness = avgFitness,
            maxFitness = maxFitness,
            avgCheckpoints = avgCheckpoints,
            maxCheckpoints = maxCheckpoints,
            totalLaps = totalLaps,
            avgSpeed = avgSpeed,
            trainingPhase = phase
        };
        
        // Add to history
        trainingHistory.Add(metric);
        
        // Log to CSV file
        LogToCSV(metric);
        
        // Log detailed car data if enabled
        if (saveDetailedCarData)
        {
            LogDetailedCarData();
        }
        
        Debug.Log($"Logged training data - Gen: {metric.generation}, Episode: {metric.episode}, " +
                 $"Avg Fitness: {metric.avgFitness:F2}, Max Fitness: {metric.maxFitness:F2}");
    }
    
    private void LogToCSV(TrainingMetric metric)
    {
        string filepath = Path.Combine(logDirectory, "training_metrics.csv");
        bool fileExists = File.Exists(filepath);
        
        using (StreamWriter writer = new StreamWriter(filepath, true))
        {
            // Write header if file is new
            if (!fileExists)
            {
                writer.WriteLine("Timestamp,Generation,Episode,AvgFitness,MaxFitness,AvgCheckpoints,MaxCheckpoints,TotalLaps,AvgSpeed,Phase");
            }
            
            // Write data row
            writer.WriteLine($"{metric.timestamp:F1},{metric.generation},{metric.episode}," +
                           $"{metric.avgFitness:F2},{metric.maxFitness:F2}," +
                           $"{metric.avgCheckpoints:F2},{metric.maxCheckpoints:F0}," +
                           $"{metric.totalLaps},{metric.avgSpeed:F2},{metric.trainingPhase}");
        }
    }
    
    private void LogDetailedCarData()
    {
        string filepath = Path.Combine(logDirectory, $"car_data_gen{geneticManager.currentGeneration}.csv");
        bool fileExists = File.Exists(filepath);
        
        using (StreamWriter writer = new StreamWriter(filepath, true))
        {
            // Write header if file is new
            if (!fileExists)
            {
                writer.WriteLine("Timestamp,Generation,CarIndex,Fitness,Checkpoints,Laps,Speed,Distance");
            }
            
            // Write data for each car
            for (int i = 0; i < geneticManager.population.Count; i++)
            {
                var genome = geneticManager.population[i];
                writer.WriteLine($"{Time.time:F1},{geneticManager.currentGeneration},{i}," +
                               $"{genome.fitness:F2},{genome.checkpointsPassed}," +
                               $"{genome.lapsCompleted},{genome.avgSpeed:F2},{genome.totalDistance:F2}");
            }
        }
    }
    
    // Public method to get training history for visualization
    public List<TrainingMetric> GetTrainingHistory()
    {
        return trainingHistory;
    }
    
    // Method to save all training history data
    public void SaveTrainingHistory()
    {
        string filepath = Path.Combine(logDirectory, "full_training_history.json");
        string json = JsonUtility.ToJson(new TrainingHistoryContainer { metrics = trainingHistory.ToArray() }, true);
        File.WriteAllText(filepath, json);
        Debug.Log($"Saved complete training history to {filepath}");
    }
    
    // Container class for serialization
    [System.Serializable]
    private class TrainingHistoryContainer
    {
        public TrainingMetric[] metrics;
    }
}