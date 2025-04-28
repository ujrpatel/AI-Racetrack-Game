using UnityEngine;

public class LapTracker : MonoBehaviour
{
    [Header("UI only in Editor")]
    public bool showGui = true;
    float lapTime, bestTime = float.MaxValue;
    int lapNum;

    void OnEnable()
    {
        RaceEvents.OnLapStarted   += (_,n)   => lapNum=n;
        RaceEvents.OnLapCompleted += (_,n,t,_)=> { lapTime=t; bestTime=Mathf.Min(bestTime,t); };
    }
    void OnGUI()
    {
#if !UNITY_WEBGL
        if (!showGui || Application.isBatchMode) return;
        GUI.Label(new Rect(10,10,200,20), $"Lap:{lapNum}");
        GUI.Label(new Rect(10,30,200,20), $"Time:{lapTime:F2}s");
        GUI.Label(new Rect(10,50,200,20), $"Best:{bestTime:F2}s");
#endif
    }
}
