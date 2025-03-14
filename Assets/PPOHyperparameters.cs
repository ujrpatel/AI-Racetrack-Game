using UnityEngine;

[System.Serializable]
public class PPOHyperparameters
{
    // Learning parameters
    [Tooltip("Learning rate for the neural network")]
    [Range(0.0001f, 0.01f)]
    public float learningRate = 0.0003f;
    
    [Tooltip("Discount factor for future rewards")]
    [Range(0.8f, 0.999f)]
    public float gamma = 0.99f;
    
    [Tooltip("GAE parameter for advantage estimation")]
    [Range(0.8f, 0.999f)]
    public float lambda = 0.95f;
    
    [Tooltip("Clip parameter for PPO")]
    [Range(0.1f, 0.3f)]
    public float epsilon = 0.2f;
    
    [Tooltip("Number of epochs to train on each batch")]
    [Range(1, 10)]
    public int numEpochs = 4;
    
    [Tooltip("Batch size for training")]
    [Range(32, 2048)]
    public int batchSize = 64;
    
    [Tooltip("Buffer size for experience collection")]
    [Range(256, 10240)]
    public int bufferSize = 2048;
    
    [Tooltip("Entropy coefficient")]
    [Range(0.0f, 0.1f)]
    public float entropyCoef = 0.01f;
    
    [Tooltip("Value function coefficient")]
    [Range(0.5f, 1.0f)]
    public float valueCoef = 0.5f;
    
    // Network architecture
    [Tooltip("Sizes of hidden layers")]
    public int[] hiddenUnits = new int[] { 128, 64 };
    
    [Tooltip("Activation function (0=Tanh, 1=ReLU)")]
    [Range(0, 1)]
    public int activation = 1;
    
    // Genetic algorithm mutation methods
    public PPOHyperparameters Clone()
    {
        PPOHyperparameters clone = new PPOHyperparameters
        {
            learningRate = this.learningRate,
            gamma = this.gamma,
            lambda = this.lambda,
            epsilon = this.epsilon,
            numEpochs = this.numEpochs,
            batchSize = this.batchSize,
            bufferSize = this.bufferSize,
            entropyCoef = this.entropyCoef,
            valueCoef = this.valueCoef,
            activation = this.activation
        };
        
        // Deep copy the hidden units array
        clone.hiddenUnits = new int[this.hiddenUnits.Length];
        System.Array.Copy(this.hiddenUnits, clone.hiddenUnits, this.hiddenUnits.Length);
        
        return clone;
    }
    
    public void Mutate(float mutationRate, float mutationMagnitude)
    {
        if (Random.value < mutationRate) 
            learningRate *= Random.Range(1f - mutationMagnitude, 1f + mutationMagnitude);
        
        if (Random.value < mutationRate) 
            gamma = Mathf.Clamp(gamma + Random.Range(-0.05f, 0.05f) * mutationMagnitude, 0.8f, 0.999f);
        
        if (Random.value < mutationRate) 
            lambda = Mathf.Clamp(lambda + Random.Range(-0.05f, 0.05f) * mutationMagnitude, 0.8f, 0.999f);
        
        if (Random.value < mutationRate) 
            epsilon = Mathf.Clamp(epsilon + Random.Range(-0.05f, 0.05f) * mutationMagnitude, 0.1f, 0.3f);
        
        if (Random.value < mutationRate) 
            numEpochs = Mathf.Clamp(numEpochs + Mathf.RoundToInt(Random.Range(-2f, 2f) * mutationMagnitude), 1, 10);
        
        if (Random.value < mutationRate) 
            batchSize = Mathf.RoundToInt(batchSize * Random.Range(0.5f, 1.5f) * mutationMagnitude);
        batchSize = Mathf.Clamp(batchSize, 32, 2048);
        // Make sure batchSize is a power of 2
        batchSize = Mathf.ClosestPowerOfTwo(batchSize);
        
        if (Random.value < mutationRate) 
            bufferSize = Mathf.RoundToInt(bufferSize * Random.Range(0.5f, 1.5f) * mutationMagnitude);
        bufferSize = Mathf.Clamp(bufferSize, 256, 10240);
        
        if (Random.value < mutationRate) 
            entropyCoef = Mathf.Clamp(entropyCoef + Random.Range(-0.005f, 0.005f) * mutationMagnitude, 0f, 0.1f);
        
        if (Random.value < mutationRate) 
            valueCoef = Mathf.Clamp(valueCoef + Random.Range(-0.1f, 0.1f) * mutationMagnitude, 0.5f, 1f);
        
        // Mutation for neural network architecture (less frequent)
        if (Random.value < mutationRate * 0.5f)
        {
            // Change layer size
            int layerToChange = Random.Range(0, hiddenUnits.Length);
            int currentSize = hiddenUnits[layerToChange];
            float sizeChange = Random.Range(0.5f, 1.5f) * mutationMagnitude;
            hiddenUnits[layerToChange] = Mathf.Max(16, Mathf.RoundToInt(currentSize * sizeChange));
        }
        
        if (Random.value < mutationRate * 0.2f)
        {
            // Switch activation function occasionally
            activation = activation == 0 ? 1 : 0;
        }
    }

    public static PPOHyperparameters Crossover(PPOHyperparameters parent1, PPOHyperparameters parent2)
    {
        PPOHyperparameters child = new PPOHyperparameters();
        
        // Simple crossover - randomly select parameters from either parent
        child.learningRate = Random.value < 0.5f ? parent1.learningRate : parent2.learningRate;
        child.gamma = Random.value < 0.5f ? parent1.gamma : parent2.gamma;
        child.lambda = Random.value < 0.5f ? parent1.lambda : parent2.lambda;
        child.epsilon = Random.value < 0.5f ? parent1.epsilon : parent2.epsilon;
        child.numEpochs = Random.value < 0.5f ? parent1.numEpochs : parent2.numEpochs;
        child.batchSize = Random.value < 0.5f ? parent1.batchSize : parent2.batchSize;
        child.bufferSize = Random.value < 0.5f ? parent1.bufferSize : parent2.bufferSize;
        child.entropyCoef = Random.value < 0.5f ? parent1.entropyCoef : parent2.entropyCoef;
        child.valueCoef = Random.value < 0.5f ? parent1.valueCoef : parent2.valueCoef;
        child.activation = Random.value < 0.5f ? parent1.activation : parent2.activation;
        
        // For neural network architecture, we'll take the average size of layers
        // First ensure both parents have same number of layers (take the smaller one)
        int layerCount = Mathf.Min(parent1.hiddenUnits.Length, parent2.hiddenUnits.Length);
        child.hiddenUnits = new int[layerCount];
        
        for (int i = 0; i < layerCount; i++)
        {
            if (Random.value < 0.5f)
            {
                // Take directly from one parent
                child.hiddenUnits[i] = Random.value < 0.5f ? parent1.hiddenUnits[i] : parent2.hiddenUnits[i];
            }
            else
            {
                // Or blend between parents
                float blend = Random.value;
                child.hiddenUnits[i] = Mathf.RoundToInt(
                    parent1.hiddenUnits[i] * blend + parent2.hiddenUnits[i] * (1 - blend)
                );
                child.hiddenUnits[i] = Mathf.Max(16, child.hiddenUnits[i]); // Ensure minimum size
            }
        }
        
        return child;
    }
}