using UnityEngine;
using System;
// Central event system for Track-related events
// Decouples components by allowing them to communicate via events rather than direct references
public static class RaceEvents
{
    // Checkpoint events
    public static event Action<GameObject, int> OnCheckpointPassed;
    
    // Lap events
    public static event Action<GameObject, int> OnLapStarted;
    public static event Action<GameObject, int, float, float> OnLapCompleted; // vehicle, lapNum, time, avgSpeed
    
    // Race state events
    public static event Action<GameObject> OnVehicleReset;
    public static event Action<GameObject, bool> OnControlModeChanged; // vehicle, isManual
    public static event Action<bool> OnTrainingStateChanged; // isPaused
    
    // Collision events
    public static event Action<GameObject, GameObject> OnVehicleCollision; // vehicle, collisionObject
    
    // Invoke methods - these are called by the actual implementation components
    
    public static void CheckpointPassed(GameObject vehicle, int checkpointIndex)
    {
        OnCheckpointPassed?.Invoke(vehicle, checkpointIndex);
    }
    
    public static void LapStarted(GameObject vehicle, int lapNumber)
    {
        OnLapStarted?.Invoke(vehicle, lapNumber);
    }
    
    public static void LapCompleted(GameObject vehicle, int lapNumber, float lapTime, float avgSpeed)
    {
        OnLapCompleted?.Invoke(vehicle, lapNumber, lapTime, avgSpeed);
    }
    
    public static void VehicleReset(GameObject vehicle)
    {
        OnVehicleReset?.Invoke(vehicle);
    }
    
    public static void ControlModeChanged(GameObject vehicle, bool isManual)
    {
        OnControlModeChanged?.Invoke(vehicle, isManual);
    }
    
    public static void TrainingStateChanged(bool isPaused)
    {
        OnTrainingStateChanged?.Invoke(isPaused);
    }
    
    public static void VehicleCollision(GameObject vehicle, GameObject collidedWith)
    {
        OnVehicleCollision?.Invoke(vehicle, collidedWith);
    }
}
