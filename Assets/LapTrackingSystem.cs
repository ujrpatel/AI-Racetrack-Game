using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Unity.MLAgents;
using System;
using System.IO;

public class LapTrackingSystem : MonoBehaviour
{
    [Header("Checkpoint QTY & completion %")]

    // [Header("Record output")]
    public string runId = "default";
    // If true, each run will overwrite its CSV; otherwise appends.
    public bool overwriteEachRun = false;

    public SplineTrackGenerator trackGenerator;
    [Range(0.5f,1f)] public float requiredCheckpointCompletion = 1f;

    // Per‐vehicle data
    class Data { 
      public int lap; 
      public float lapStart; 
      public int visited; 
      public float distance; 
      public Vector3 lastPos; 
    }
    private Dictionary<GameObject, Data> vehicles = new Dictionary<GameObject, Data>();

    // Records
    [Header("Record output")]
    public bool saveToFile = true;
    private string filePath;

    void Awake()
    {
        var args = Environment.GetCommandLineArgs();
        foreach (var a in args)
        {
            if (a.StartsWith("--run-id="))
            {
                runId = a.Substring("--run-id=".Length);
                break;
            }
        }
        // 2) Build the Lap Records folder next to your project root (sibling to Assets/)
        var recordsFolder = Path.Combine(Application.dataPath, "../Lap Records");
        Directory.CreateDirectory(recordsFolder);

        var fileName = $"{runId}_lap_records.csv";
        filePath = Path.Combine(recordsFolder, fileName);

        if (overwriteEachRun && File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        RaceEvents.OnCheckpointPassed += OnCP;
        RaceEvents.OnLapStarted += OnLapStart;
        RaceEvents.OnLapCompleted += OnLapComplete;

    }

    void OnDestroy()
    {
        RaceEvents.OnCheckpointPassed -= OnCP;
        RaceEvents.OnLapStarted -= OnLapStart;
        RaceEvents.OnLapCompleted -= OnLapComplete;

    }

    void OnCP(GameObject v, int idx)
    {
        if (!vehicles.TryGetValue(v, out var d))
        {
            d = new Data { lap = 1, lapStart = Time.time, lastPos = v.transform.position };
            vehicles[v] = d;
        }
        d.visited++;
    }

    void OnLapStart(GameObject v, int lapNumber)
    {
        if (!vehicles.TryGetValue(v, out var d))
            vehicles[v] = d = new Data();
        d.lap = lapNumber;
        d.lapStart = Time.time;
        d.visited = 0;
        d.distance = 0;
        d.lastPos = v.transform.position;
    }

    void OnLapComplete(GameObject v, int lapNumber, float lapTime, float avgSpeed)
    {
        if (!vehicles.TryGetValue(v, out var d))
            return;

        // Save record
        if (saveToFile)
        {
            bool exists = File.Exists(filePath);
            using var w = new StreamWriter(filePath, true);
            if (!exists)
                w.WriteLine("Vehicle,Lap,Time,AvgSpeed,Distance");
            w.WriteLine($"{v.name},{lapNumber},{lapTime:F3},{avgSpeed:F2},{d.distance:F2}");
        }
        Debug.Log($"[LapTracking] {v.name} lap{lapNumber}={lapTime:F2}s @ {avgSpeed:F2}m/s");

        // TensorBoard stats
        var stats = Academy.Instance.StatsRecorder;
        stats.Add("lap_time", lapTime);
        stats.Add("lap_avg_speed", avgSpeed);
    }

    void Update()
    {
        // Track distance each frame
        foreach (var kv in vehicles)
        {
            var agent = kv.Key;
            var d = kv.Value;
            if (agent == null || agent.gameObject == null) continue;
            var pos = agent.transform.position;
            d.distance += Vector3.Distance(pos, d.lastPos);
            d.lastPos = pos;
        }
    }
}
