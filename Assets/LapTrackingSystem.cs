using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class LapTrackingSystem : MonoBehaviour
{
    [Header("Checkpoint QTY & completion %")]
    public SplineTrackGenerator trackGenerator;
    [Range(0.5f,1f)] public float requiredCheckpointCompletion = 1f;

    // Per‐vehicle data
    class Data { 
      public int lap=0; 
      public float lapStart; 
      public int visited; 
      public float distance; 
      public Vector3 lastPos; 
    }
    Dictionary<int,Data> vehicles = new();

    // Records
    [Header("Record output")]
    public bool saveToFile = true;
    string filePath;

    void Awake()
    {
        filePath = System.IO.Path.Combine(
            Application.persistentDataPath, "lap_records.csv"
        );
        RaceEvents.OnCheckpointPassed += OnCP;
        RaceEvents.OnLapStarted      += OnLapStart;
        RaceEvents.OnLapCompleted    += OnLapComplete;
        RaceEvents.OnVehicleReset    += OnReset;
    }

    void OnDestroy()
    {
        RaceEvents.OnCheckpointPassed -= OnCP;
        RaceEvents.OnLapStarted      -= OnLapStart;
        RaceEvents.OnLapCompleted    -= OnLapComplete;
        RaceEvents.OnVehicleReset    -= OnReset;
    }

    void OnCP(GameObject v, int idx)
    {
        int id = v.GetInstanceID();
        if (!vehicles.ContainsKey(id))
            vehicles[id] = new Data { lapStart=Time.time, lastPos=v.transform.position };
        vehicles[id].visited++;
    }

    void OnLapStart(GameObject v, int lapNumber)
    {
        var d = vehicles[v.GetInstanceID()];
        d.lap = lapNumber;
        d.lapStart = Time.time;
        d.visited = 0;
        d.distance = 0;
        d.lastPos = v.transform.position;
    }

    void OnLapComplete(GameObject v, int lapNumber, float lapTime, float avgSpeed)
    {
        // Write to CSV
        if (saveToFile)
        {
            bool exists = System.IO.File.Exists(filePath);
            using var w = new System.IO.StreamWriter(filePath, true);
            if (!exists)
                w.WriteLine("Vehicle,Lap,Time,AvgSpeed");
            w.WriteLine($"{v.name},{lapNumber},{lapTime:F3},{avgSpeed:F2}");
        }
        Debug.Log($"[LapTracking] {v.name} lap{lapNumber}={lapTime:F2}s @ {avgSpeed:F2}m/s");
    }

    void OnReset(GameObject v)
    {
        // Treat a reset like a new lap start
        RaceEvents.LapStarted(v, vehicles[v.GetInstanceID()].lap + 1);
    }

    void Update()
    {
        // Track distance each frame
        foreach (var kv in vehicles)
        {
            var v = EditorUtility.InstanceIDToObject(kv.Key) as GameObject;
            if (v == null) continue;
            var d = kv.Value;
            d.distance += Vector3.Distance(v.transform.position, d.lastPos);
            d.lastPos = v.transform.position;
        }
    }
}
