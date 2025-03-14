using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class TrainingVisualization : MonoBehaviour
{
    public TrainingCoordinator coordinator;
    public GeneticPPOManager geneticManager;
    
    // UI References
    public Text generationText;
    public Text episodeText;
    public Text fitnessText;
    public Text hyperparametersText;
    public Text statusText;
    
    // Visualization
    public RectTransform graphContainer;
    public GameObject linePrefab;
    public GameObject pointPrefab;
    
    private List<float> avgFitnessHistory = new List<float>();
    private List<float> bestFitnessHistory = new List<float>();
    private List<GameObject> graphObjects = new List<GameObject>();
    
    void Update()
    {
        UpdateUIText();
        
        // Update graph on generation change
        if (geneticManager != null && 
            geneticManager.currentEpisode == 1 && 
            geneticManager.currentGeneration > bestFitnessHistory.Count)
        {
            UpdateGraph();
        }
    }
    
    void UpdateUIText()
    {
        if (coordinator == null || geneticManager == null) return;
        
        if (generationText != null)
        {
            generationText.text = $"Generation: {geneticManager.currentGeneration}";
        }
        
        if (episodeText != null)
        {
            episodeText.text = $"Episode: {geneticManager.currentEpisode}/{geneticManager.geneticParams.generationEpisodes}";
        }
        
        if (fitnessText != null)
        {
            // Find current best
            float bestFitness = 0;
            foreach (var genome in geneticManager.population)
            {
                if (genome.fitness > bestFitness)
                {
                    bestFitness = genome.fitness;
                }
            }
            
            fitnessText.text = $"Best Fitness: {bestFitness:F2}";
        }
        
        if (hyperparametersText != null && geneticManager.activeGenomeIndex >= 0)
        {
            // Display active genome's hyperparameters
            var activeGenome = geneticManager.population[geneticManager.activeGenomeIndex];
            
            hyperparametersText.text = 
                $"Learning Rate: {activeGenome.policyParams.learningRate:F6}\n" +
                $"Gamma: {activeGenome.policyParams.gamma:F4}\n" +
                $"Lambda: {activeGenome.policyParams.lambda:F4}\n" +
                $"Epsilon: {activeGenome.policyParams.epsilon:F4}\n" +
                $"Epochs: {activeGenome.policyParams.numEpochs}";
        }
        
        if (statusText != null && coordinator != null)
        {
            statusText.text = coordinator.statusText;
        }
    }
    
    void UpdateGraph()
    {
        if (geneticManager == null || graphContainer == null) return;
        
        // Calculate statistics for the last generation
        float totalFitness = 0;
        float maxFitness = 0;
        
        foreach (var genome in geneticManager.population)
        {
            totalFitness += genome.fitness;
            if (genome.fitness > maxFitness)
            {
                maxFitness = genome.fitness;
            }
        }
        
        float avgFitness = totalFitness / geneticManager.population.Count;
        
        // Add to history
        avgFitnessHistory.Add(avgFitness);
        bestFitnessHistory.Add(maxFitness);
        
        // Clear existing graph
        foreach (var obj in graphObjects)
        {
            Destroy(obj);
        }
        graphObjects.Clear();
        
        // Draw new graph
        DrawGraph();
    }
    
    void DrawGraph()
    {
        if (graphContainer == null || bestFitnessHistory.Count < 2) return;
        
        // Find max value for scaling
        float maxValue = 0;
        foreach (float value in bestFitnessHistory)
        {
            if (value > maxValue) maxValue = value;
        }
        maxValue = Mathf.Max(maxValue, 1f); // Avoid division by zero
        
        // Graph dimensions
        float width = graphContainer.rect.width;
        float height = graphContainer.rect.height;
        float xUnit = width / (bestFitnessHistory.Count - 1);
        
        // Draw best fitness line
        Vector2 prevPointBest = Vector2.zero;
        for (int i = 0; i < bestFitnessHistory.Count; i++)
        {
            float x = i * xUnit;
            float y = (bestFitnessHistory[i] / maxValue) * height;
            
            Vector2 currentPoint = new Vector2(x, y);
            
            // Create point
            GameObject point = Instantiate(pointPrefab, graphContainer);
            point.GetComponent<RectTransform>().anchoredPosition = currentPoint;
            graphObjects.Add(point);
            
            // Create line (except for first point)
            if (i > 0)
            {
                GameObject line = Instantiate(linePrefab, graphContainer);
                Image lineImage = line.GetComponent<Image>();
                lineImage.color = Color.green;
                
                RectTransform rectTransform = line.GetComponent<RectTransform>();
                
                // Set position and size
                Vector2 direction = currentPoint - prevPointBest;
                float distance = direction.magnitude;
                
                rectTransform.anchoredPosition = prevPointBest + direction * 0.5f;
                rectTransform.sizeDelta = new Vector2(distance, 2f);
                
                // Set rotation
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                rectTransform.rotation = Quaternion.Euler(0, 0, angle);
                
                graphObjects.Add(line);
            }
            
            prevPointBest = currentPoint;
        }
        
        // Draw average fitness line
        Vector2 prevPointAvg = Vector2.zero;
        for (int i = 0; i < avgFitnessHistory.Count; i++)
        {
            float x = i * xUnit;
            float y = (avgFitnessHistory[i] / maxValue) * height;
            
            Vector2 currentPoint = new Vector2(x, y);
            
            // Create point
            GameObject point = Instantiate(pointPrefab, graphContainer);
            point.GetComponent<RectTransform>().anchoredPosition = currentPoint;
            point.GetComponent<Image>().color = Color.blue;
            graphObjects.Add(point);
            
            // Create line (except for first point)
            if (i > 0)
            {
                GameObject line = Instantiate(linePrefab, graphContainer);
                Image lineImage = line.GetComponent<Image>();
                lineImage.color = Color.blue;
                
                RectTransform rectTransform = line.GetComponent<RectTransform>();
                
                // Set position and size
                Vector2 direction = currentPoint - prevPointAvg;
                float distance = direction.magnitude;
                
                rectTransform.anchoredPosition = prevPointAvg + direction * 0.5f;
                rectTransform.sizeDelta = new Vector2(distance, 2f);
                
                // Set rotation
                float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
                rectTransform.rotation = Quaternion.Euler(0, 0, angle);
                
                graphObjects.Add(line);
            }
            
            prevPointAvg = currentPoint;
        }
        
        // Add labels
        GameObject bestLabel = new GameObject("BestLabel", typeof(RectTransform), typeof(Text));
        bestLabel.transform.SetParent(graphContainer);
        bestLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(10, height - 20);
        Text bestText = bestLabel.GetComponent<Text>();
        bestText.text = "Best Fitness";
        bestText.color = Color.green;
        bestText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        bestText.fontSize = 14;
        graphObjects.Add(bestLabel);
        
        GameObject avgLabel = new GameObject("AvgLabel", typeof(RectTransform), typeof(Text));
        avgLabel.transform.SetParent(graphContainer);
        avgLabel.GetComponent<RectTransform>().anchoredPosition = new Vector2(10, height - 40);
        Text avgText = avgLabel.GetComponent<Text>();
        avgText.text = "Average Fitness";
        avgText.color = Color.blue;
        avgText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        avgText.fontSize = 14;
        graphObjects.Add(avgLabel);
    }
}