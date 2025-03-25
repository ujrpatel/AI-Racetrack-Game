using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using System.Collections.Generic;
using System.Collections;
using System.Linq;

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
    
    // Flag to prevent race conditions during episode advancement
    private bool isAdvancingEpisode = false;
    private object advancementLock = new object();
    
    void Start()
    {
        Debug.Log("[GeneticPPOManager] Starting initialization...");
        
        // Check for required components
        if (carAgentPrefab == null)
            Debug.LogError("[GeneticPPOManager] Car agent prefab is missing!");
        
        if (spawnPoint == null)
            Debug.LogWarning("[GeneticPPOManager] Spawn point is missing, will use origin (0,0,0)");
        
        if (trackGenerator == null)
            Debug.LogError("[GeneticPPOManager] Track generator is missing!");
        else
            Debug.Log($"[GeneticPPOManager] Track generator assigned: {trackGenerator.name}");
        
        // Check if track has checkpoints
        if (trackGenerator != null)
        {
            var checkpoints = trackGenerator.GetCheckpoints();
            if (checkpoints != null && checkpoints.Count > 0)
                Debug.Log($"[GeneticPPOManager] Found {checkpoints.Count} checkpoints on the track");
            else
                Debug.LogError("[GeneticPPOManager] No checkpoints found on the track! Cars will spawn at the origin.");
        }
        
        InitializePopulation();
    }
    
    private void ConfigureRigidbody(Rigidbody rb)
    {
        rb.mass = 1000f;
        rb.linearDamping = 0.05f;
        rb.angularDamping = 0.7f;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rb.centerOfMass = new Vector3(0, -0.5f, 0);
    }
    
    private CarGenome CreateNewGenome()
    {
        Debug.Log($"[GeneticPPOManager] Creating new genome {population.Count}");
        
        // Safety checks
        if (carAgentPrefab == null)
        {
            Debug.LogError("[GeneticPPOManager] Car agent prefab is null!");
            return null;
        }
        
        if (spawnPoint == null)
        {
            Debug.LogWarning("[GeneticPPOManager] Spawn point is null, using origin");
        }
        
        Vector3 spawnPosition = spawnPoint != null ? spawnPoint.position : Vector3.zero;
        Quaternion spawnRotation = spawnPoint != null ? spawnPoint.rotation : Quaternion.identity;
        
        // Instantiate car agent (initially off-track to avoid collisions)
        GameObject agentObject = Instantiate(carAgentPrefab, spawnPosition + Vector3.up * 100f, spawnRotation);
        agentObject.name = $"Car_Gen{currentGeneration}_Genome{population.Count}";
        
        // Ensure components are properly added
        Rigidbody rb = agentObject.GetComponent<Rigidbody>();
        if (rb == null) 
        {
            Debug.Log("[GeneticPPOManager] Adding missing Rigidbody to car agent");
            rb = agentObject.AddComponent<Rigidbody>();
            ConfigureRigidbody(rb);
        }
        
        CarController controller = agentObject.GetComponent<CarController>();
        if (controller == null)
        {
            Debug.Log("[GeneticPPOManager] Adding missing CarController to car agent");
            controller = agentObject.AddComponent<CarController>();
        }

        // Get the ML-Agents components
        CarAgent agent = agentObject.GetComponent<CarAgent>();
        if (agent == null)
        {
            Debug.Log("[GeneticPPOManager] Adding missing CarAgent to car agent");
            agent = agentObject.AddComponent<CarAgent>();
        }

        // Assign references
        if (trackGenerator != null)
        {
            agent.trackGenerator = trackGenerator;
            Debug.Log("[GeneticPPOManager] Assigned track generator to car agent");
        }
        else
        {
            Debug.LogWarning("[GeneticPPOManager] No track generator to assign to car agent");
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
        
        Debug.Log($"[GeneticPPOManager] Created new genome {population.Count - 1}");
        return genome;
    }
    
    [Header("Multiple Car Training")]
    public bool useSimultaneousTraining = true;
    public int maxSimultaneousCars = 5;
    public float carSpacing = 50f; // Distance between cars on the track
    public List<int> activeGenomeIndices = new List<int>();
    
    // Car dimensions for better spacing
    public float carWidth = 2.0f;
    public float carLength = 4.0f;
    public float minCarSpacing = 5.0f; // Minimum distance between cars

    void InitializePopulation()
    {
        Debug.Log("[GeneticPPOManager] Initializing population...");
        
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
            Debug.Log("[GeneticPPOManager] Using simultaneous training with multiple cars");
            SetupSimultaneousTraining();
        }
        else
        {
            Debug.Log("[GeneticPPOManager] Using sequential training with single car");
            SetActiveGenome(0);
        }
        
        currentGeneration = 1;
        currentEpisode = 1;
        Debug.Log($"[GeneticPPOManager] Generation {currentGeneration}, Episode {currentEpisode} started");
    }

    public void NotifyGenomeComplete(CarAgent agent)
    {
        // Find the genome for this agent
        CarGenome genome = population.FirstOrDefault(g => g.agent == agent);
        if (genome != null)
        {
            Debug.Log($"[GeneticPPOManager] Agent {agent.name} completed its episode");
            UpdateGenomeFitness(genome);
            
            // Use lock to prevent race conditions
            lock (advancementLock)
            {
                // Check if all genomes are complete (and we're not already advancing)
                if (!isAdvancingEpisode)
                {
                    bool allComplete = true;
                    foreach (int index in activeGenomeIndices)
                    {
                        if (index < population.Count)
                        {
                            GameObject obj = population[index].agentInstance;
                            if (obj != null && obj.activeSelf)
                            {
                                allComplete = false;
                                break;
                            }
                        }
                    }
                    
                    if (allComplete)
                    {
                        Debug.Log("[GeneticPPOManager] All active genomes have completed their episodes");
                        // Set flag to prevent multiple calls
                        isAdvancingEpisode = true;
                        
                        // Use coroutine to advance training
                        StartCoroutine(AdvanceTrainingCoroutine());
                    }
                    else
                    {
                        Debug.Log("[GeneticPPOManager] Waiting for other genomes to complete their episodes");
                    }
                }
            }
        }
        else
        {
            Debug.LogWarning($"[GeneticPPOManager] Could not find genome for agent {agent.name}");
        }
    }

    private IEnumerator AdvanceTrainingCoroutine()
    {
        // Wait for end of frame to ensure all physics and agent updates are complete
        yield return new WaitForEndOfFrame();
        
        // Now advance the training
        AdvanceTraining();
        
        // Reset the flag
        isAdvancingEpisode = false;
    }

    // Improved simultaneous training setup with better car distribution
private void SetupSimultaneousTraining()
{
    Debug.Log("[GeneticPPOManager] Setting up simultaneous training...");
    activeGenomeIndices.Clear();
    
    if (population == null || population.Count == 0)
    {
        Debug.LogError("[GeneticPPOManager] Cannot set up training: Population is empty");
        return;
    }
    
    // Determine how many cars to activate
    int carsToActivate = Mathf.Min(maxSimultaneousCars, population.Count);
    Debug.Log($"[GeneticPPOManager] Setting up {carsToActivate} cars for simultaneous training");
    
    // Get track information
    if (trackGenerator != null)
    {
        List<Transform> checkpoints = trackGenerator.GetCheckpoints();
        
        if (checkpoints != null && checkpoints.Count > 0)
        {
            // Important: Distribute cars evenly along the track at different checkpoints
            for (int i = 0; i < carsToActivate; i++)
            {
                // Calculate checkpoint index for this car - ensure even distribution
                int checkpointIndex = (i * checkpoints.Count / carsToActivate) % checkpoints.Count;
                Transform checkpoint = checkpoints[checkpointIndex];
                
                // Calculate next checkpoint
                int nextCheckpointIndex = (checkpointIndex + 1) % checkpoints.Count;
                
                // Calculate spawn position with height offset to avoid ground collision
                Vector3 spawnPosition = checkpoint.position + Vector3.up * 0.5f;
                
                // Stagger cars across track width to avoid collisions
                float roadWidth = trackGenerator.roadWidth;
                float safeWidth = roadWidth * 0.7f;
                float laneOffset = ((i % 3) - 1) * (safeWidth / 3); // Left, center, right
                spawnPosition += checkpoint.right * laneOffset;
                
                // Offset slightly forward to ensure it doesn't collide with checkpoint
                spawnPosition += checkpoint.forward * 2.0f;
                
                // Get car from population
                GameObject carObject = population[i].agentInstance;
                
                if (carObject != null)
                {
                    // Position the car
                    carObject.transform.position = spawnPosition;
                    carObject.transform.rotation = checkpoint.rotation;
                    carObject.SetActive(true);
                    
                    // Set the next checkpoint
                    CarAgent agent = population[i].agent;
                    if (agent != null)
                    {
                        agent.SetNextCheckpointIndex(nextCheckpointIndex);
                        agent.OnEpisodeBegin(); // Reset the agent
                        
                        // Register with PPO trainer
                        if (ppoTrainer != null)
                        {
                            ppoTrainer.RegisterAgent(agent);
                            ppoTrainer.ApplyGenomeToConfig(population[i]);
                        }
                        
                        // Add to active indices
                        activeGenomeIndices.Add(i);
                        
                        // Reset metrics for this episode
                        population[i].episodeStartTime = Time.time;
                        population[i].initialCheckpointsPassed = 0;
                        population[i].initialLapsCompleted = 0;
                    }
                }
            }
        }
        else
        {
            Debug.LogError("[GeneticPPOManager] No checkpoints found on track!");
        }
    }
    else
    {
        Debug.LogError("[GeneticPPOManager] No track generator assigned!");
    }
}

    // Add method to calculate exploration rate based on generation
    private float GetExplorationRate(int generation)
    {
        // High exploration in early generations, decreasing over time
        float baseRate = 0.4f; // 40% random actions initially
        float minRate = 0.05f; // 5% minimum exploration
        float decayFactor = 0.2f; // How quickly exploration decreases
        
        return Mathf.Max(minRate, baseRate * Mathf.Exp(-decayFactor * (generation - 1)));
    }

    public void OnEpisodeComplete()
    {
        // Method kept for backward compatibility, but we're using NotifyGenomeComplete now
        Debug.Log("[GeneticPPOManager] OnEpisodeComplete called via deprecated method");
    }

    private void AdvanceTraining()
    {
        Debug.Log("[GeneticPPOManager] Advancing training...");
        
        currentEpisode++;
        Debug.Log($"[GeneticPPOManager] Moving to episode {currentEpisode}");
        
        if (currentEpisode > geneticParams.generationEpisodes)
        {
            Debug.Log("[GeneticPPOManager] End of generation reached, evolving population");
            EvolvePopulation();
            currentEpisode = 1;
            currentGeneration++;
            Debug.Log($"[GeneticPPOManager] Generation {currentGeneration} started");
        }
        
        // Setup next episode
        if (useSimultaneousTraining)
        {
            Debug.Log("[GeneticPPOManager] Setting up next episode with simultaneous training");
            SetupSimultaneousTraining();
        }
        else
        {
            int nextGenomeIndex = (activeGenomeIndex + 1) % population.Count;
            Debug.Log($"[GeneticPPOManager] Setting up next episode with sequential training, genome {nextGenomeIndex}");
            SetActiveGenome(nextGenomeIndex);
        }
        
        Debug.Log($"[GeneticPPOManager] Generation {currentGeneration}, Episode {currentEpisode} setup complete");
    }

    public void SetActiveGenome(int index)
    {
        Debug.Log($"[GeneticPPOManager] Setting active genome to index {index}");
        
        // Safety check
        if (index < 0 || index >= population.Count)
        {
            Debug.LogError($"[GeneticPPOManager] Invalid genome index: {index}, population size: {population.Count}");
            return;
        }
        
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
                if (agent == null)
                {
                    Debug.LogError($"[GeneticPPOManager] Missing CarAgent component on genome {index}");
                    return;
                }
                
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
                
                Debug.Log($"[GeneticPPOManager] Active genome set to {index} with parameters: LR={population[index].policyParams.learningRate:F6}");
                
                // Set initial exploration mode for early training
                if (currentGeneration == 1 && currentEpisode <= 2)
                {
                    agent.forceExploration = true;
                    agent.explorationThrottle = 0.8f;
                    Debug.Log($"[GeneticPPOManager] Forcing exploration for genome {index} (early training)");
                }
                else
                {
                    agent.forceExploration = false;
                }
            }
            else
            {
                Debug.Log($"[GeneticPPOManager] Genome {index} is not active for this episode");
            }
        }
    }
    
    private void UpdateGenomeFitness(CarGenome genome)
    {
        if (genome == null || genome.agent == null) return;
        
        // Get fitness from the agent
        float calculatedFitness = genome.agent.CalculateFitness();
        
        // Update genome metrics
        genome.checkpointsPassed = genome.agent.GetCheckpointsPassed();
        genome.lapsCompleted = genome.agent.GetLapsCompleted();
        genome.totalDistance = genome.agent.GetTotalDistance();
        
        // Get episode time
        float episodeTime = Time.time - genome.episodeStartTime;
        if (episodeTime < 0.1f) episodeTime = 0.1f; // Avoid division by zero
        
        // Calculate average speed
        float avgSpeed = episodeTime > 1f ? genome.totalDistance / episodeTime : 0;
        
        // Update overall metrics
        genome.avgSpeed = (genome.avgSpeed * (currentEpisode - 1) + avgSpeed) / currentEpisode;
        
        // Update fitness (average across episodes)
        genome.fitness = (genome.fitness * (currentEpisode - 1) + calculatedFitness) / currentEpisode;
        
        Debug.Log($"[GeneticPPOManager] Genome {population.IndexOf(genome)} fitness: {genome.fitness:F2} " +
                  $"(Checkpoints: {genome.checkpointsPassed}, " +
                  $"Laps: {genome.lapsCompleted}, " +
                  $"Avg Speed: {genome.avgSpeed:F2})");
    }
    
    private void EvolvePopulation()
    {
        Debug.Log("[GeneticPPOManager] Evolving population...");
        
        // Sort population by fitness
        population = population.OrderByDescending(g => g.fitness).ToList();
        
        // Print fitness stats
        Debug.Log("=== GENERATION RESULTS ===");
        Debug.Log($"Generation {currentGeneration} completed");
        for (int i = 0; i < population.Count; i++)
        {
            Debug.Log($"Genome {i}: Fitness={population[i].fitness:F2}, " +
                     $"Checkpoints={population[i].checkpointsPassed}, " +
                     $"Laps={population[i].lapsCompleted}, " +
                     $"Speed={population[i].avgSpeed:F2}");
        }
        
        // Calculate generation metrics
        float avgFitness = (float)population.Average(g => g.fitness);
        float maxFitness = (float)population.Max(g => g.fitness);
        int totalLaps = population.Sum(g => g.lapsCompleted);
        float avgCheckpoints = (float)population.Average(g => g.checkpointsPassed);
        
        Debug.Log($"Generation Stats: Avg Fitness={avgFitness:F2}, " +
                 $"Max Fitness={maxFitness:F2}, " +
                 $"Total Laps={totalLaps}, " +
                 $"Avg Checkpoints={avgCheckpoints:F2}");
        
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
            
            Debug.Log($"[GeneticPPOManager] Elite {i} kept with fitness {elite.fitness:F2}");
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
            
            Debug.Log($"[GeneticPPOManager] Created new offspring (index {newPopulation.Count-1})");
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
        
        // Save progress after each generation
        SaveTrainingProgress();
        
        Debug.Log($"[GeneticPPOManager] Evolution complete. New population size: {population.Count}");
    }
    
    private void SaveTrainingProgress()
    {
        Debug.Log("[GeneticPPOManager] Saving training progress...");
        try
        {
            // Create directory if it doesn't exist
            string savePath = "Assets/TrainingData";
            if (!System.IO.Directory.Exists(savePath))
            {
                System.IO.Directory.CreateDirectory(savePath);
            }
            
            // Save generation metrics to CSV
            string metricsPath = $"{savePath}/training_metrics.csv";
            bool fileExists = System.IO.File.Exists(metricsPath);
            
            using (System.IO.StreamWriter writer = new System.IO.StreamWriter(metricsPath, true))
            {
                // Write header if file is new
                if (!fileExists)
                {
                    writer.WriteLine("Generation,AvgFitness,MaxFitness,AvgCheckpoints,TotalLaps,AvgSpeed");
                }
                
                // Calculate metrics
                float avgFitness = (float)population.Average(g => g.fitness);
                float maxFitness = (float)population.Max(g => g.fitness);
                float avgCheckpoints = (float)population.Average(g => g.checkpointsPassed);
                int totalLaps = population.Sum(g => g.lapsCompleted);
                float avgSpeed = (float)population.Average(g => g.avgSpeed);
                
                // Write metrics line
                writer.WriteLine($"{currentGeneration},{avgFitness:F2},{maxFitness:F2},{avgCheckpoints:F2},{totalLaps},{avgSpeed:F2}");
            }
            
            // Save best genome parameters
            CarGenome bestGenome = population.OrderByDescending(g => g.fitness).First();
            string paramsPath = $"{savePath}/best_params_gen{currentGeneration}.json";
            string paramsJson = JsonUtility.ToJson(bestGenome.policyParams, true);
            System.IO.File.WriteAllText(paramsPath, paramsJson);
            
            Debug.Log($"[GeneticPPOManager] Saved training progress for generation {currentGeneration}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[GeneticPPOManager] Failed to save training progress: {e.Message}");
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

    private void OnDrawGizmos() {
    // Only draw when in Play mode and cars are being positioned
    if (!Application.isPlaying || activeGenomeIndices.Count == 0) return;
    
    Gizmos.color = Color.green;
    foreach (int index in activeGenomeIndices) {
        if (index < population.Count && population[index].agentInstance != null) {
            Vector3 pos = population[index].agentInstance.transform.position;
            // Draw car bounding box
            Gizmos.DrawWireCube(pos, new Vector3(carWidth, 1f, carLength));
            // Draw car forward direction
            Gizmos.color = Color.blue;
            Gizmos.DrawRay(pos, population[index].agentInstance.transform.forward * 3f);
            Gizmos.color = Color.green;
        }
    }
}

    
    private CarGenome CrossoverGenomes(CarGenome parent1, CarGenome parent2)
    {
        Debug.Log($"[GeneticPPOManager] Performing crossover between genome {population.IndexOf(parent1)} and {population.IndexOf(parent2)}");
        
        // Create new agent instance
        GameObject agentObject = Instantiate(carAgentPrefab, spawnPoint.position, spawnPoint.rotation);
        
        agentObject.name = $"Car_Gen{currentGeneration + 1}_Offspring{population.Count}";
        agentObject.SetActive(false);
        
        CarAgent agent = agentObject.GetComponent<CarAgent>();
        if (agent == null)
        {
            Debug.LogError("[GeneticPPOManager] New car agent is missing CarAgent component!");
            agent = agentObject.AddComponent<CarAgent>();
        }
        
        // Assign track generator if available
        if (trackGenerator != null)
        {
            agent.trackGenerator = trackGenerator;
        }
        
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
            Debug.Log("[GeneticPPOManager] Performed hyperparameter crossover");
        }
        else
        {
            // If no crossover, inherit from the fitter parent
            bool useParent1 = parent1.fitness >= parent2.fitness;
            child.policyParams = useParent1 ? parent1.policyParams.Clone() : parent2.policyParams.Clone();
            Debug.Log($"[GeneticPPOManager] No crossover, inherited from parent {(useParent1 ? "1" : "2")}");
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