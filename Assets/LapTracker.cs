using UnityEngine;

public class LapTracker : MonoBehaviour
{
    [Header("UI only in Editor")]
    public bool showGui = true;

    // References
    TrainingManager _tm;
    CarAgent _main;

    int _totalCheckpoints;
    int _checkpointsPassed;
    int _currentLap;
    float _lapStartTime;
    float _lastLapTime;
    float _bestLapTime = float.MaxValue;

    void Awake()
    {
        // grab the track manager so we know how many CPs there are
        _tm = FindFirstObjectByType<TrainingManager>();
        if (_tm != null) _totalCheckpoints = _tm.GetCheckpointCount();
    }

    void OnEnable()
    {
        // find your “main” agent in the scene using the new API
        var agents = FindObjectsByType<CarAgent>(
            FindObjectsSortMode.None
        );
        foreach (var a in agents)
            if (a.isMainAgent)
            {
                _main = a;
                break;
            }

        RaceEvents.OnLapStarted += HandleLapStarted;
        RaceEvents.OnLapCompleted += HandleLapCompleted;
        RaceEvents.OnCheckpointPassed += HandleCheckpointPassed;
    }

    void OnDisable()
    {
        RaceEvents.OnLapStarted      -= HandleLapStarted;
        RaceEvents.OnLapCompleted    -= HandleLapCompleted;
        RaceEvents.OnCheckpointPassed-= HandleCheckpointPassed;
    }
    void HandleLapStarted(GameObject v, int lapNumber)
    {
        if (_main == null || v != _main.gameObject) return;
        _currentLap        = lapNumber;
        _lapStartTime      = Time.time;
        _checkpointsPassed = 0;
    }
    void HandleLapCompleted(GameObject v, int lapNumber, float lapTime, float avgSpeed)
    {
        if (_main == null || v != _main.gameObject) return;
        _lastLapTime = lapTime;
        _bestLapTime = Mathf.Min(_bestLapTime, lapTime);
        // the next lap will start automatically via OnLapStarted,
        // but we keep currentLap in sync in case we want to display it
        _currentLap = lapNumber + 1;
    }

    void HandleCheckpointPassed(GameObject v, int idx)
    {
        if (_main == null || v != _main.gameObject) return;
        _checkpointsPassed++;
    }

    void OnGUI()
    {
#if !UNITY_WEBGL
        if (!showGui || Application.isBatchMode || _main == null) return;

        var style = new GUIStyle
        {
            fontSize  = 14,
            alignment = TextAnchor.UpperLeft
        };
        style.normal.textColor = Color.black;

        const float w = 200, h = 20;
        float x = Screen.width - w - 10;
        float y = 10;

        // Lap #
        GUI.Label(new Rect(x, y, w, h), $"Lap: {_currentLap}", style);
        y += h;

        // Live time
        float live = Time.time - _lapStartTime;
        GUI.Label(new Rect(x, y, w, h), $"Time: {live:F2}s", style);
        y += h;

        // Last lap
        GUI.Label(new Rect(x, y, w, h), $"Last: {_lastLapTime:F2}s", style);
        y += h;

        // Best lap
        GUI.Label(new Rect(x, y, w, h), $"Best: {_bestLapTime:F2}s", style);
        y += h;

        // Checkpoints
        GUI.Label(new Rect(x, y, w, h), $"CP: {_checkpointsPassed}/{_totalCheckpoints}", style);
#endif
    }
}
