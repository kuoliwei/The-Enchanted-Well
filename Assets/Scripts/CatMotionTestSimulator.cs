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

    [Header("Unstable Motion Capture Simulation")]
    [SerializeField]
    private bool unstableMode = false;

    [SerializeField, Tooltip("每 N 幀模擬一次『沒有人』")]
    private int dropEveryNFrames = 10;

    private int unstableFrameCounter = 0;

    [SerializeField]
    private int dropDurationFrames = 1;

    private bool isDropping = false;
    private int remainingDropFrames = 0;

    // 重用容器，避免每幀 GC
    //private readonly List<float> testAngles = new List<float>(1);
    //private readonly List<float> testPercents = new List<float>(1);

    // 重用容器，避免每幀 GC
    private readonly List<float> testAngles = new List<float>();
    private readonly List<float> testPercents = new List<float>();


    private void Update()
    {
        if (liveUpdate && catManager != null)
        {
            Apply();
        }
    }

    public void Apply()
    {
        if (catManager == null)
            return;

        if (!unstableMode)
        {
            unstableFrameCounter = 0;
            isDropping = false;
            remainingDropFrames = 0;
        }

        testAngles.Clear();
        testPercents.Clear();

        bool simulateDropThisFrame = false;

        if (unstableMode)
        {
            unstableFrameCounter++;

            // 如果目前正在斷訊中
            if (isDropping)
            {
                simulateDropThisFrame = true;
                remainingDropFrames--;

                if (remainingDropFrames <= 0)
                {
                    isDropping = false;
                }
            }
            // 尚未斷訊，檢查是否該開始一次新的斷訊
            else if (dropEveryNFrames > 0 &&
                     unstableFrameCounter % dropEveryNFrames == 0)
            {
                isDropping = true;
                remainingDropFrames = Mathf.Max(1, dropDurationFrames);
                simulateDropThisFrame = true;
            }
        }


        if (simulateDropThisFrame)
        {
            // 模擬：這一幀動捕完全沒抓到人
            // → 直接送空資料
            catManager.HandleSkeletonData(testAngles, testPercents);
        }
        else
        {
            // ★ 原本的穩定行為（一行不改）
            int count = Mathf.Min(angles.Count, skeletonPercentStates.Count);

            for (int i = 0; i < count; i++)
            {
                testAngles.Add(angles[i]);
                testPercents.Add((float)skeletonPercentStates[i]);
            }

            catManager.HandleSkeletonData(testAngles, testPercents);
        }
    }

}
