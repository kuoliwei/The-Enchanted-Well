using UnityEngine;
using PoseSocket;
using System.Collections.Generic;
using UnityEngine.Video;

[System.Serializable]
public class CatVideoSet
{
    public VideoClip[] clips;
}

public class CatManager : MonoBehaviour
{
    [Header("Data Source")]
    [SerializeField] private PoseDataReceiver poseReceiver;

    [Header("Prefab（包含貓控制器與影片播放器）")]
    [SerializeField] private CatMotionController catPrefab;

    [Header("貓生成的 UI Parent")]
    [SerializeField] private RectTransform catParent;

    [Header("貓花色影片組")]
    [SerializeField] private List<CatVideoSet> catVideoSets;

    [Header("資料中斷判定秒數")]
    [SerializeField] private float timeoutSeconds = 0.5f;

    private readonly List<CatMotionController> cats = new List<CatMotionController>();
    private float lastFrameTime = 0f;
    //private Dictionary<int, int> slotToPersonIndex = new Dictionary<int, int>();
    // 尚未被使用的貓影片 index 池（不重複抽）
    private List<int> availableCatIndices = new List<int>();

    // 記錄每隻貓使用的是哪一個影片 index（用來回收）
    private Dictionary<CatMotionController, int> catToVideoIndex
        = new Dictionary<CatMotionController, int>();

    [Header("Angle Snap Settings")]
    [SerializeField]
    private int[] snapAngles = { 0, 60, 120, 180, 240, 300, 360 };

    // ======== ★ 新增：角度切換 threshold 設定 ========
    [Header("Angle Switch Threshold")]
    [SerializeField]
    private float angleSwitchThreshold = 20f;

    // ======== ★ 新增：記錄每個人的上一個 slot ========
    private Dictionary<int, int> personLastSlot = new Dictionary<int, int>();

    // ======== ★ 新增：skeletonPercent 變更門檻設定 ========
    [Header("Skeleton Percent Switch Threshold")]
    [SerializeField]
    private int skeletonPercentThresholdCount = 30;   // 連續幾筆才允許變更（可在 Inspector 調整）

    // 每個人的 skeleton 百分比穩定狀態
    private class SkeletonPercentCounter
    {
        public float currentValue = 0f;   // 目前已採用的穩定數值（0 / 50 / 100）
        public float pendingValue = 0f;   // 正在嘗試切換的新數值
        public int pendingCount = 0;      // pendingValue 已連續出現幾次
        public bool initialized = false;  // 是否已初始化
    }

    // personIndex → SkeletonPercentCounter
    private Dictionary<int, SkeletonPercentCounter> skeletonPercentCounters
        = new Dictionary<int, SkeletonPercentCounter>();

    private void Start()
    {
        catToVideoIndex.Clear();
        ResetAvailableCats(); // 初始化可抽貓池
        if (poseReceiver != null)
            poseReceiver.OnSkeletonFrame += OnSkeletonFrame;
        else
            Debug.LogError("[CatManager] poseReceiver 未設定");
    }

    private void Update()
    {
        // 資料中斷偵測
        if (Time.time - lastFrameTime > timeoutSeconds)
        {
            if (cats.Count > 0)
            {
                ClearAllCatsImmediate();
                Debug.Log("[CatManager] 資料中斷，立即清空所有貓");
            }
        }
    }
    private void ResetAvailableCats()
    {
        availableCatIndices.Clear();
        catToVideoIndex.Clear();

        for (int i = 0; i < catVideoSets.Count; i++)
            availableCatIndices.Add(i);
    }
    private int DrawRandomUnusedCatIndex()
    {
        if (availableCatIndices.Count == 0)
        {
            Debug.LogWarning("[CatManager] 沒有可用的貓可以分配");
            return -1;
        }

        int r = Random.Range(0, availableCatIndices.Count);
        int catIndex = availableCatIndices[r];

        // 抽走就移除，確保不重複
        availableCatIndices.RemoveAt(r);

        return catIndex;
    }
    // 立即刪除，不等 frame 結束
    private void ClearAllCatsImmediate()
    {
        for (int i = 0; i < cats.Count; i++)
            DestroyImmediate(cats[i].gameObject);

        cats.Clear();
        personLastSlot.Clear(); // 避免舊角度殘留
        skeletonPercentCounters.Clear(); // 同步清空百分比狀態
        ResetAvailableCats(); // 重置可抽貓池
    }

    private void EnsureCatCount(int count)
    {
        // 生成不足的貓
        while (cats.Count < count)
        {
            var newCat = Instantiate(catPrefab, catParent);
            AssignRandomCatVideoSet(newCat);
            cats.Add(newCat);
        }

        // 刪除多餘的貓
        //while (cats.Count > count)
        //{
        //    DestroyImmediate(cats[cats.Count - 1].gameObject);
        //    cats.RemoveAt(cats.Count - 1);
        //}
        while (cats.Count > count)
        {
            var lastCat = cats[cats.Count - 1];

            // 回收這隻貓使用的影片 index
            if (catToVideoIndex.TryGetValue(lastCat, out int usedIndex))
            {
                if (!availableCatIndices.Contains(usedIndex))
                    availableCatIndices.Add(usedIndex);

                catToVideoIndex.Remove(lastCat);
            }

            DestroyImmediate(lastCat.gameObject);
            cats.RemoveAt(cats.Count - 1);
        }

    }

    private void AssignRandomCatVideoSet(CatMotionController cat)
    {
        var vp = cat.GetComponentInChildren<CatVideoPlayerController>();

        if (vp == null)
        {
            Debug.LogWarning("[CatManager] 找不到 CatVideoPlayerController");
            return;
        }

        if (catVideoSets == null || catVideoSets.Count == 0)
        {
            Debug.LogWarning("[CatManager] catVideoSets 是空的");
            return;
        }

        //int index = Random.Range(0, catVideoSets.Count);
        int index = DrawRandomUnusedCatIndex();
        if (index < 0)
            return;
        vp.videoClips = catVideoSets[index].clips;
        // 記錄這隻貓使用的影片 index（之後刪除時要回收）
        catToVideoIndex[cat] = index;
    }

    private void OnSkeletonFrame(SkeletonFrame frame)
    {
        if (frame == null)
            return;

        lastFrameTime = Time.time;
        HandleSkeletonData(frame.angles, frame.skeletonPercent);
    }

    public void HandleSkeletonData(
        List<float> angles,
        List<float> skeletonPercent
    )
    {
        int personCount = angles.Count;
        if (personCount <= 0)
        {
            ClearAllCatsImmediate();
            return;
        }
        // 移除已離場的 personIndex（避免殘留狀態）
        var removeList = new List<int>();
        foreach (var key in personLastSlot.Keys)
        {
            if (key >= personCount)
                removeList.Add(key);
        }

        for (int i = 0; i < removeList.Count; i++)
        {
            personLastSlot.Remove(removeList[i]);
        }

        removeList.Clear();

        //slotToPersonIndex.Clear();

        // 依順位處理每個人
        for (int i = 0; i < personCount; i++)
        {
            float rawAngle = angles[i];

            // 座標轉換（原樣保留）
            float ext = Mathf.Repeat(rawAngle, 360f);
            float internalAngle = 270f - ext;
            if (internalAngle < 0f) internalAngle += 360f;

            // 量化到 slot
            int best = snapAngles[0];
            float bestDist = Mathf.Abs(internalAngle - snapAngles[0]);

            for (int s = 1; s < snapAngles.Length; s++)
            {
                float d = Mathf.Abs(internalAngle - snapAngles[s]);
                if (d < bestDist)
                {
                    bestDist = d;
                    best = snapAngles[s];
                }
            }

            if (best == 360)
                best = 0;

            // ======== ★ 新增：角度切換 threshold ========
            int finalSlot = best;

            if (personLastSlot.TryGetValue(i, out int lastSlot))
            {
                float distFromLast = Mathf.Abs(internalAngle - lastSlot);
                if (distFromLast < angleSwitchThreshold)
                {
                    finalSlot = lastSlot;
                }
            }

            personLastSlot[i] = finalSlot;
            best = finalSlot;
            // ============================================

            // 同一角度只允許一隻貓（原樣保留）
            //if (slotToPersonIndex.ContainsKey(best))
            //    continue;

            //slotToPersonIndex.Add(best, i);
            //slotToPersonIndex[best] = i;
        }

        // 確保貓數量 = 有效 slot 數
        //EnsureCatCount(slotToPersonIndex.Count);
        EnsureCatCount(personCount);

        //int catIndex = 0;
        //foreach (var kvp in slotToPersonIndex)
        //{
        //    int personIndex = kvp.Value;

        //    // 先經過門檻機制，取得「穩定後」的百分比
        //    float stablePercent = GetStableSkeletonPercent(
        //        personIndex,
        //        skeletonPercent[personIndex]
        //    );

        //    cats[catIndex].UpdateHeadPosition(stablePercent);
        //    cats[catIndex].UpdateAngle(kvp.Key);

        //    catIndex++;
        //}

        for (int personIndex = 0; personIndex < personCount; personIndex++)
        {
            float stablePercent = GetStableSkeletonPercent(
                personIndex,
                skeletonPercent[personIndex]
            );

            int slot = personLastSlot[personIndex];

            cats[personIndex].UpdateHeadPosition(stablePercent);
            cats[personIndex].UpdateAngle(slot);
        }
    }
    public void NotifySimulationFrame()
    {
        lastFrameTime = Time.time;
    }

    /// <summary>
    /// 根據「連續出現次數」決定是否允許切換 skeletonPercent。
    /// - 每個 personIndex 各自有獨立的 counter。
    /// - 只有當 newValue 連續出現達到 skeletonPercentThresholdCount 次時，
    ///   才會真的切換 currentValue。
    /// - 回傳值 = 目前允許使用的穩定數值。
    /// </summary>
    private float GetStableSkeletonPercent(int personIndex, float newValue)
    {
        if (!skeletonPercentCounters.TryGetValue(personIndex, out var counter))
        {
            // 第一次看到這個人：強制從 0 開始
            counter = new SkeletonPercentCounter
            {
                currentValue = 0f,
                pendingValue = newValue,
                pendingCount = 1,
                initialized = true
            };

            skeletonPercentCounters[personIndex] = counter;
            return counter.currentValue; // 一開始一定回傳 0
        }

        // 如果新值跟目前穩定值一樣 → 無須切換，重置 pending 計數
        if (Mathf.Approximately(newValue, counter.currentValue))
        {
            counter.pendingValue = newValue;
            counter.pendingCount = 0;
            return counter.currentValue;
        }

        // 新值與目前穩定值不同：檢查 pending 狀態
        if (!Mathf.Approximately(newValue, counter.pendingValue))
        {
            // 換了一個新的候選值，重新計數
            counter.pendingValue = newValue;
            counter.pendingCount = 1;
        }
        else
        {
            // 候選值與上次相同，增加連續次數
            counter.pendingCount++;
        }

        // 尚未達到門檻 → 不切換，維持原本的 currentValue
        if (counter.pendingCount < skeletonPercentThresholdCount)
            return counter.currentValue;

        // 達到門檻 → 正式切換
        counter.currentValue = counter.pendingValue;
        counter.pendingCount = 0; // 重置計數，等待下一次變化

        return counter.currentValue;
    }

}
