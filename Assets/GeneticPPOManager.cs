using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
//test
public class GeneticPPOManager : MonoBehaviour
{
    [System.Serializable]
    public class GeneticParameters
    {
        public float mutationRate = 0.1f;
        public float mutationMagnitude = 0.2f;
        public float crossoverRate = 0.7f;
        public int populationSize = 10;
        public int eliteCount = 2;
        public int generationEpisodes = 5;
        public float fitnessWeightSpeed = 1.0f;
        public float fitnessWeightCheckpoints = 5.0f;
        public float fitnessWeightLaps = 10.0f;
    }
    
    public GeneticParameters geneticParams;
    public PPOHyperparameters defaultPPOParams;
    
    public GameObject carAgentPrefab;
    public Transform spawnPoint;
    public SplineTrackGenerator trackGenerator;
    
    [HideInInspector]
    public List<CarGenome> population = new List<CarGenome>();
    [HideInInspector]
    public int currentGeneration = 0;
    [HideInInspector]
    public int currentEpisode = 0;
    [HideInInspector]
    public int activeGenomeIndex = -1;
    
    // Reference to the PPO-GA trainer
    public PPOGATrainer ppoTrainer;
    
    void Start()
    {
        InitializePopulation();
    }
    
    
    private CarGenome CreateNewGenome()
{
    // Safety checks
    if (carAgentPrefab == null)
    {
        Debug.LogError("Car agent prefab is null!");
        return null;
    }
    
    if (spawnPoint == null)
    {
        Debug.LogWarning("Spawn point is null, using scene origin");
    }
    
    Vector3 spawnPosition = spawnPoint != null ? spawnPoint.position : Vector3.zero;
    Quaternion spawnRotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;
    
    // Calculate an offset to avoid overlaps during initialization
    Vector3 spawnOffset = new Vector3(0, 0, -5f * population.Count); // 5 meter spacing between cars
    
    try
    {
        // Instantiate car agent with offset
        GameObject agentObject = Instantiate(carAgentPrefab, spawnPosition + spawnOffset, spawnRotation);
        agentObject.name = $"Car_Gen{currentGeneration}_Genome{population.Count}";
        
        // Ensure components are properly added
        Rigidbody rb = agentObject.GetComponent<Rigidbody>();
        if (rb == null) 
        {
            rb = agentObject.AddComponent<Rigidbody>();
            rb.mass = 1000;
            rb.linearDamping = 0.1f;
            rb.angularDamping = 0.5f;
        }
        
        CarController controller = agentObject.GetComponent<CarController>();
        if (controller == null) controller = agentObject.AddComponent<CarController>();
        
        // Get the ML-Agents components
        CarAgent agent = agentObject.GetComponent<CarAgent>();
        if (agent == null) agent = agentObject.AddComponent<CarAgent>();
        
        // Assign references
        if (trackGenerator != null)
        {
            agent.trackGenerator = trackGenerator;
        }
        
        // Setup genome
        CarGenome genome = new CarGenome
        {
            agentInstance = agentObject,
            agent = agent,
            policyParams = defaultPPOParams != null ? defaultPPOParams.Clone() : new PPOHyperparameters(),
            fitness = 0,
            checkpointsPassed = 0,
            lapsCompleted = 0,
            avgSpeed = 0,
            totalDistance = 0
        };
        
        // Randomize parameters for genetic diversity (except for first genome which uses defaults)
        if (population.Count > 0 && genome.policyParams != null)
        {
            genome.policyParams.Mutate(0.5f, 0.3f); // High initial mutation for diversity
        }
        
        // Apply hyperparameters to the agent
        if (ppoTrainer != null)
        {
            ppoTrainer.ApplyGenomeToConfig(genome);
        }
        
        // Disable initially
        agentObject.SetActive(false);
        
        // Add to population
        population.Add(genome);
        
        return genome;
    }
    catch (System.Exception e)
    {
        Debug.LogError($"Error creating new genome: {e.Message}\n{e.StackTrace}");
        return null;
    }
}
    
    [Header("Multiple Car Training")]
public bool useSimultaneousTraining = true;
public int maxSimultaneousCars = 5;
public float carSpacing = 50f; // Distance between cars on the track
private List<int> activeGenomeIndices = new List<int>();

// Modify InitializePopulation method
void InitializePopulation()
{
    // Clear existing population
    foreach (var genome in population)
    {
        if (genome.agentInstance != null)
        {
            Destroy(genome.agentInstance);
        }
    }
    population.Clear();
    activeGenomeIndices.Clear();
    
    // Create initial population
    for (int i = 0; i < geneticParams.populationSize; i++)
    {
        CreateNewGenome();
    }
    
    // Start first episode with multiple cars if enabled
    if (useSimultaneousTraining)
    {
        SetupSimultaneousTraining();
    }
    else
    {
        SetActiveGenome(0);
    }
    
    currentGeneration = 1;
    currentEpisode = 1;
    Debug.Log($"Generation {currentGeneration}, Episode {currentEpisode} started");
}

// Add this new method
private void SetupSimultaneousTraining()
{
    activeGenomeIndices.Clear();
    
    // Safety check
    if (population == null || population.Count == 0)
    {
        Debug.LogError("Cannot setup simultaneous training: Population is empty or null");
        return;
    }
    
    // Make sure we have a track generator and spawnPoint
    if (trackGenerator == null)
    {
        Debug.LogWarning("TrackGenerator is null, using default spawn position only");
        // Fall back to single agent training at spawn point
        if (population.Count > 0)
        {
            SetActiveGenome(0);
            activeGenomeIndices.Add(0);
        }
        return;
    }
    
    // Verify checkpoints exist
    var checkpoints = trackGenerator.GetCheckpoints();
    if (checkpoints == null || checkpoints.Count == 0)
    {
        Debug.LogWarning("No checkpoints found, using default spawn position only");
        // Fall back to single agent training at spawn point
        if (population.Count > 0)
        {
            SetActiveGenome(0);
            activeGenomeIndices.Add(0);
        }
        return;
    }
    
    // Determine how many cars to activate
    int carsToActivate = Mathf.Min(maxSimultaneousCars, population.Count);
    
    // Calculate spacing based on track length
    float trackLength = checkpoints.Count * 20f; // Rough estimation
    float spacing = trackLength / Mathf.Max(2, carsToActivate);
    
    // Activate the cars with appropriate spacing
    for (int i = 0; i < carsToActivate; i++)
    {
        try
        {
            SetActiveGenomeWithOffset(i, i * spacing);
            activeGenomeIndices.Add(i);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error setting up genome {i}: {e.Message}\n{e.StackTrace}");
        }
    }
    
    Debug.Log($"Activated {activeGenomeIndices.Count} cars for simultaneous training with spacing {spacing}m");
}

// Add this new method
private void SetActiveGenomeWithOffset(int index, float trackOffset)
{
    if (index < 0 || index >= population.Count) 
    {
        Debug.LogError($"Invalid genome index: {index}, population size: {population.Count}");
        return;
    }
    
    // Get the agent instance
    GameObject agentObject = population[index].agentInstance;
    if (agentObject == null)
    {
        Debug.LogError($"Agent instance for genome {index} is null");
        return;
    }
    
    // Calculate position along the track
    Vector3 position = spawnPoint != null ? spawnPoint.position : Vector3.zero;
    Quaternion rotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;
    
    // If we have checkpoints, use them to determine offset position
    if (trackGenerator != null)
    {
        var checkpoints = trackGenerator.GetCheckpoints();
        if (checkpoints != null && checkpoints.Count > 0)
        {
            // Find the appropriate checkpoint based on offset
            float trackLength = checkpoints.Count * 20f; // Rough estimation
            float normalizedOffset = trackLength > 0 ? (trackOffset % trackLength) / trackLength : 0;
            int checkpointIndex = Mathf.FloorToInt(normalizedOffset * checkpoints.Count);
            
            if (checkpointIndex < checkpoints.Count)
            {
                position = checkpoints[checkpointIndex].position;
                rotation = checkpoints[checkpointIndex].rotation;
                
                // Add a small height offset to prevent cars from being underground
                position.y += 0.5f;
            }
        }
    }
    
    // Enable the car
    agentObject.SetActive(true);
    
    // Position the car
    agentObject.transform.position = position;
    agentObject.transform.rotation = rotation;
    
    // Safety check for agent component
    CarAgent agent = population[index].agent;
    if (agent == null)
    {
        Debug.LogError($"CarAgent component for genome {index} is null");
        agent = agentObject.GetComponent<CarAgent>();
        if (agent == null)
        {
            Debug.LogError($"Failed to get CarAgent component for genome {index}");
            return;
        }
        population[index].agent = agent;
    }
    
    // Set trackGenerator reference if needed
    if (agent.trackGenerator == null && trackGenerator != null)
    {
        agent.trackGenerator = trackGenerator;
    }
    
    // Reset agent for this episode
    agent.OnEpisodeBegin();
    
    // Set up tracking metrics
    population[index].episodeStartTime = Time.time;
    population[index].initialCheckpointsPassed = population[index].checkpointsPassed;
    population[index].initialLapsCompleted = population[index].lapsCompleted;
    
    // Configure for training
    if (ppoTrainer != null)
    {
        ppoTrainer.RegisterAgent(agent);
        ppoTrainer.ApplyGenomeToConfig(population[index]);
    }
    
    // Enable exploration for early training
    if (currentGeneration == 1 && currentEpisode <= 2)
    {
        agent.forceExploration = true;
        agent.explorationThrottle = 0.8f;
    }
    else
    {
        agent.forceExploration = false;
    }
    
    Debug.Log($"Positioned genome {index} on track with offset {trackOffset}m");
}

// Update OnEpisodeComplete to handle simultaneous training
public void OnEpisodeComplete()
{
    if (useSimultaneousTraining)
    {
        // Check if all active genomes have completed their episodes
        bool allComplete = true;
        
        foreach (int genomeIndex in activeGenomeIndices)
        {
            // Update fitness for each active genome
            UpdateGenomeFitness(population[genomeIndex]);
            
            // Check if this genome is still active (agent might not be done yet)
            if (population[genomeIndex].agentInstance.activeSelf)
            {
                allComplete = false;
            }
        }
        
        if (allComplete)
        {
            // Move to next episode or generation
            currentEpisode++;
            if (currentEpisode > geneticParams.generationEpisodes)
            {
                EvolvePopulation();
                currentEpisode = 1;
                currentGeneration++;
                Debug.Log($"Generation {currentGeneration} started");
            }
            
            // Setup new batch of cars
            SetupSimultaneousTraining();
        }
    }
    else
    {
        // Original single-car logic
        if (activeGenomeIndex < 0 || activeGenomeIndex >= population.Count) return;
        
        UpdateGenomeFitness(population[activeGenomeIndex]);
        
        currentEpisode++;
        if (currentEpisode > geneticParams.generationEpisodes)
        {
            EvolvePopulation();
            currentEpisode = 1;
            currentGeneration++;
            Debug.Log($"Generation {currentGeneration} started");
        }
        
        int nextGenomeIndex = (activeGenomeIndex + 1) % population.Count;
        SetActiveGenome(nextGenomeIndex);
        
        Debug.Log($"Generation {currentGeneration}, Episode {currentEpisode}, Genome {nextGenomeIndex} started");
    }
}

    public void SetActiveGenome(int index)
    {
        // Instead of disabling all genomes, we'll use a different approach
        // We'll keep track of which genomes are active for this generation/episode
        
        // First, determine how many cars we want active at once (can be adjusted)
        int simultaneousCars = Mathf.Min(5, population.Count); // Maximum of 5 cars at once
        
        // Calculate if this car should be active
        bool shouldBeActive = index < simultaneousCars;
        
        // Set active state for just this car
        if (index >= 0 && index < population.Count)
        {
            population[index].agentInstance.SetActive(shouldBeActive);
            
            if (shouldBeActive)
            {
                activeGenomeIndex = index;
                
                // Reset car and tracking metrics
                CarAgent agent = population[index].agent;
                agent.OnEpisodeBegin();
                
                // Reset fitness tracking for this episode
                population[index].episodeStartTime = Time.time;
                population[index].initialCheckpointsPassed = population[index].checkpointsPassed;
                population[index].initialLapsCompleted = population[index].lapsCompleted;
                
                if (ppoTrainer != null)
                {
                    ppoTrainer.RegisterAgent(agent);
                    ppoTrainer.ApplyGenomeToConfig(population[index]);
                }
                
                Debug.Log($"Active genome set to {index} with parameters: LR={population[index].policyParams.learningRate:F6}");
                
                // Set initial exploration mode for early training
                if (currentGeneration == 1 && currentEpisode <= 2)
                {
                    agent.forceExploration = true;
                    agent.explorationThrottle = 0.8f;
                }
                else
                {
                    agent.forceExploration = false;
                }
            }
        }
    }
    
    
    
    private IEnumerator ForceInitialMovement(CarAgent agent)
{
    // Apply throttle for a few seconds to get the car moving
    for (int i = 0; i < 100; i++)
    {
        if (agent != null && agent.carController != null)
        {
            agent.carController.Move(0, 0.8f, 0);
        }
        yield return new WaitForFixedUpdate();
    }
}
    
    
    private void UpdateGenomeFitness(CarGenome genome)
    {
        // Calculate fitness based on various metrics
        float episodeTime = Time.time - genome.episodeStartTime;
        if (episodeTime < 0.1f) episodeTime = 0.1f; // Avoid division by zero
        
        int checkpointsGained = genome.checkpointsPassed - genome.initialCheckpointsPassed;
        int lapsGained = genome.lapsCompleted - genome.initialLapsCompleted;
        
        // Update genome metrics (retrieved from agent)
        if (genome.agent != null)
        {
            genome.checkpointsPassed = genome.agent.GetCheckpointsPassed();
            genome.lapsCompleted = genome.agent.GetLapsCompleted();
            genome.totalDistance = genome.agent.GetTotalDistance();
        }
        
        // Calculate average speed 
        float avgSpeed = episodeTime > 1f ? genome.totalDistance / episodeTime : 0;
        
        // Update overall metrics
        genome.avgSpeed = (genome.avgSpeed * (currentEpisode - 1) + avgSpeed) / currentEpisode;
        
        // Calculate episode fitness
        float episodeFitness = 
            checkpointsGained * geneticParams.fitnessWeightCheckpoints +
            lapsGained * geneticParams.fitnessWeightLaps +
            avgSpeed * geneticParams.fitnessWeightSpeed;
        
        // Update overall fitness (average across episodes)
        genome.fitness = (genome.fitness * (currentEpisode - 1) + episodeFitness) / currentEpisode;
        
        Debug.Log($"Genome fitness: {genome.fitness:F2} (Checkpoints: {checkpointsGained}, Laps: {lapsGained}, Avg Speed: {avgSpeed:F2})");
    }
    
    private void EvolvePopulation()
    {
        // Sort population by fitness
        population = population.OrderByDescending(g => g.fitness).ToList();
        
        // Print fitness stats
        Debug.Log("Population fitness:");
        for (int i = 0; i < population.Count; i++)
        {
            Debug.Log($"Genome {i}: Fitness={population[i].fitness:F2}, Checkpoints={population[i].checkpointsPassed}, Laps={population[i].lapsCompleted}");
        }
        
        // Create new population
        List<CarGenome> newPopulation = new List<CarGenome>();
        
        // Keep elite individuals
        for (int i = 0; i < Mathf.Min(geneticParams.eliteCount, population.Count); i++)
        {
            // Clone the genome but keep the same agent instance
            CarGenome elite = new CarGenome
            {
                agentInstance = population[i].agentInstance,
                agent = population[i].agent,
                policyParams = population[i].policyParams.Clone(),
                fitness = population[i].fitness,
                checkpointsPassed = population[i].checkpointsPassed,
                lapsCompleted = population[i].lapsCompleted,
                avgSpeed = population[i].avgSpeed,
                totalDistance = population[i].totalDistance
            };
            
            newPopulation.Add(elite);
            
            // Rename to indicate it's an elite
            if (elite.agentInstance != null)
            {
                elite.agentInstance.name = $"Car_Gen{currentGeneration}_Elite{i}";
                elite.agentInstance.SetActive(false);
            }
        }
        
        // Fill rest of population with offspring
        while (newPopulation.Count < geneticParams.populationSize)
        {
            // Select parents using tournament selection
            CarGenome parent1 = TournamentSelection(3);
            CarGenome parent2 = TournamentSelection(3);
            
            // Create child through crossover
            CarGenome child = CrossoverGenomes(parent1, parent2);
            
            // Mutate child
            child.policyParams.Mutate(geneticParams.mutationRate, geneticParams.mutationMagnitude);
            
            // Add to new population
            newPopulation.Add(child);
        }
        
        // Remove old agents (except elites which were transferred to new population)
        for (int i = geneticParams.eliteCount; i < population.Count; i++)
        {
            if (population[i].agentInstance != null)
            {
                Destroy(population[i].agentInstance);
            }
        }
        
        // Replace old population
        population = newPopulation;
        
        // Notify trainer of generation end
        if (ppoTrainer != null)
        {
            ppoTrainer.OnGenerationEnd(currentGeneration);
        }
    }
    
    private CarGenome TournamentSelection(int tournamentSize)
    {
        // Select random individuals for tournament
        List<CarGenome> tournament = new List<CarGenome>();
        for (int i = 0; i < tournamentSize; i++)
        {
            int randomIndex = Random.Range(0, population.Count);
            tournament.Add(population[randomIndex]);
        }
        
        // Return the fittest
        return tournament.OrderByDescending(g => g.fitness).First();
    }
    
    private CarGenome CrossoverGenomes(CarGenome parent1, CarGenome parent2)
    {
        // Create new agent instance
        GameObject agentObject = Instantiate(carAgentPrefab, spawnPoint.position, spawnPoint.rotation);
        
        // Change this line - don't reference newPopulation
        agentObject.name = $"Car_Gen{currentGeneration + 1}_Offspring{population.Count}";
        agentObject.SetActive(false);
        
        // Rest of the method remains the same
        CarAgent agent = agentObject.GetComponent<CarAgent>();
        
        // Create new genome
        CarGenome child = new CarGenome
        {
            agentInstance = agentObject,
            agent = agent,
            fitness = 0,
            checkpointsPassed = 0,
            lapsCompleted = 0,
            avgSpeed = 0,
            totalDistance = 0
        };
        
        // Crossover hyperparameters
        if (Random.value < geneticParams.crossoverRate)
        {
            child.policyParams = PPOHyperparameters.Crossover(parent1.policyParams, parent2.policyParams);
        }
        else
        {
            // If no crossover, inherit from the fitter parent
            child.policyParams = parent1.fitness >= parent2.fitness ? 
                parent1.policyParams.Clone() : parent2.policyParams.Clone();
        }
        
        return child;
    }
}

[System.Serializable]
public class CarGenome
{
    public GameObject agentInstance;
    public CarAgent agent;
    public PPOHyperparameters policyParams;
    
    // Fitness metrics
    public float fitness;
    public int checkpointsPassed;
    public int lapsCompleted;
    public float avgSpeed;
    public float totalDistance;
    
    // Episode tracking
    public float episodeStartTime;
    public int initialCheckpointsPassed;
    public int initialLapsCompleted;
}