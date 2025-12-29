using System.Collections.Generic;
using UnityEngine;

public enum SkeletonPercentState
{
    Zero = 0,
    Fifty = 50,
    Hundred = 100
}
public class CatMotionTestSimulator : MonoBehaviour
{
    [Header("Target Manager")]
    [SerializeField] private CatManager catManager;

    //[Header("Test Input Values")]
    //public SkeletonPercentState skeletonPercentState = SkeletonPercentState.Zero;

    //[Range(0, 359)]
    //public float angle = 0f;

    [Header("Test Input Values")]
    public List<SkeletonPercentState> skeletonPercentStates
    = new List<SkeletonPercentState>();

    [Range(0, 359)]
    public List<float> angles
        = new List<float>();

    [Header("Auto Update")]
    public bool liveUpdate = false;

    // 重用容器，避免每幀 GC
    //private readonly List<float> testAngles = new List<float>(1);
    //private readonly List<float> testPercents = new List<float>(1);

    // 重用容器，避免每幀 GC
    private readonly List<float> testAngles = new List<float>();
    private readonly List<float> testPercents = new List<float>();


    private void Update()
    {
        if (!liveUpdate || catManager == null)
            return;

        Apply();
    }

    public void Apply()
    {
        if (catManager == null)
            return;

        testAngles.Clear();
        testPercents.Clear();

        //testAngles.Add(angle);
        //testPercents.Add((float)skeletonPercentState);

        int count = Mathf.Min(angles.Count, skeletonPercentStates.Count);

        for (int i = 0; i < count; i++)
        {
            testAngles.Add(angles[i]);
            testPercents.Add((float)skeletonPercentStates[i]);
        }

        // 正確呼叫既有入口
        catManager.HandleSkeletonData(testAngles, testPercents);

        // 同時刷新時間，避免 timeout 清貓
        catManager.NotifySimulationFrame();
    }
}
