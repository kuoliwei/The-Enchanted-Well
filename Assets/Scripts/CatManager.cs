using PoseSocket;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;

[System.Serializable]
public class CatVideoSet
{
    public VideoClip[] clips;
}

public class CatManager : MonoBehaviour
{
    public enum CatSpawnMode
    {
        PersonDriven, // 舊機制
        SlotDriven    // 新機制
    }

    [Header("Cat Spawn Mode")]
    [SerializeField]
    private CatSpawnMode spawnMode = CatSpawnMode.PersonDriven;

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

    // SlotDriven 專用：slot → cat
    private Dictionary<int, CatMotionController> slotToCat
        = new Dictionary<int, CatMotionController>();

    [Header("Angle Snap Settings")]
    [SerializeField]
    private int[] snapAngles = { 0, 60, 120, 180, 240, 300, 360 };

    [SerializeField] private int angleShift;

    // ======== ★ 角度切換 threshold 設定 ========
    [Header("Angle Switch Threshold")]
    [SerializeField]
    private float angleSwitchThreshold = 20f;

    // ======== ★ 記錄每個人的上一個 slot ========
    private Dictionary<int, int> personLastSlot = new Dictionary<int, int>();

    // ======== ★ skeletonPercent 變更門檻設定 ========
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

            //DestroyImmediate(lastCat.gameObject);
            RemoveCatWithCollapse(lastCat);
            cats.RemoveAt(cats.Count - 1);
        }

    }
    private void RemoveCatWithCollapse(CatMotionController cat)
    {
        cat.BeginSmoothCollapse();
        StartCoroutine(DestroyCatAfterDelay(cat, 1));
    }

    private IEnumerator DestroyCatAfterDelay(CatMotionController cat, float delay)
    {
        yield return new WaitForSeconds(delay);

        if (cat != null)
        {
            // 回收影片 index（關鍵）
            RecycleCatVideo(cat);

            Destroy(cat.gameObject);
        }
    }
    private void SmoothRemoveAllCats()
    {
        for (int i = cats.Count - 1; i >= 0; i--)
        {
            RemoveCatWithCollapse(cats[i]);
        }

        cats.Clear();
        personLastSlot.Clear();
        skeletonPercentCounters.Clear();
        ResetAvailableCats();
    }
    private void RecycleCatVideo(CatMotionController cat)
    {
        if (catToVideoIndex.TryGetValue(cat, out int index))
        {
            availableCatIndices.Add(index);
            catToVideoIndex.Remove(cat);
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
            if (spawnMode == CatSpawnMode.PersonDriven)
            {
                SmoothRemoveAllCats();
            }
            else if (spawnMode == CatSpawnMode.SlotDriven)
            {
                // SlotDriven 清場（重點）
                foreach (var cat in slotToCat.Values)
                {
                    RemoveCatWithCollapse(cat);
                }

                slotToCat.Clear();
            }

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

        Dictionary<int, List<int>> slotToPersons = new Dictionary<int, List<int>>();

        //slotToPersonIndex.Clear();

        // 依順位處理每個人
        for (int i = 0; i < personCount; i++)
        {
            float rawAngle = angles[i];

            // 座標轉換（原樣保留）
            float ext = Mathf.Repeat(rawAngle, 360f);
            float internalAngle = angleShift - ext;
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
                    // 還在切換門檻內 → 沿用舊 slot
                    finalSlot = lastSlot;
                }
                else
                {
                    // 真正換 slot → 重置 skeletonPercent
                    skeletonPercentCounters.Remove(i);
                }
            }

            personLastSlot[i] = finalSlot;
            best = finalSlot;

            // 記錄 slot → persons
            if (!slotToPersons.TryGetValue(finalSlot, out var list))
            {
                list = new List<int>();
                slotToPersons[finalSlot] = list;
            }
            list.Add(i);
            // ============================================

            // 同一角度只允許一隻貓（原樣保留）
            //if (slotToPersonIndex.ContainsKey(best))
            //    continue;

            //slotToPersonIndex.Add(best, i);
            //slotToPersonIndex[best] = i;
        }

        // 確保貓數量 = 有效 slot 數
        //EnsureCatCount(slotToPersonIndex.Count);
        //EnsureCatCount(personCount);

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


        if (spawnMode == CatSpawnMode.PersonDriven)
        {
            EnsureCatCount(personCount);

            for (int personIndex = 0; personIndex < personCount; personIndex++)
            {
                float stablePercent = GetStableSkeletonPercent(
                    personIndex,
                    skeletonPercent[personIndex]
                );

                int slot = personLastSlot[personIndex];
                float finalPercent = stablePercent;

                // 如果同一個 slot 有多個人，只允許順位第一的人冒頭
                if (slotToPersons.TryGetValue(slot, out var personsInSlot))
                {
                    //int allowedPerson = personsInSlot[0];
                    //for (int k = 1; k < personsInSlot.Count; k++)
                    //{
                    //    if (personsInSlot[k] < allowedPerson)
                    //        allowedPerson = personsInSlot[k];
                    //}
                    //int allowedPerson = personsInSlot[0];
                    //float bestDist = float.MaxValue;

                    //for (int k = 0; k < personsInSlot.Count; k++)
                    //{
                    //    int candidatePerson = personsInSlot[k];

                    //    float rawAngle = angles[candidatePerson];
                    //    float ext = Mathf.Repeat(rawAngle, 360f);
                    //    float internalAngle = angleShift - ext;
                    //    if (internalAngle < 0f) internalAngle += 360f;

                    //    float dist = Mathf.Abs(Mathf.DeltaAngle(internalAngle, slot));

                    //    if (dist < bestDist)
                    //    {
                    //        bestDist = dist;
                    //        allowedPerson = candidatePerson;
                    //    }
                    //}
                    int allowedPerson = GetClosestPersonToSlot(slot, personsInSlot, angles);


                    if (personIndex != allowedPerson)
                    {
                        finalPercent = 0f;
                        // 立刻強制縮頭，避免瞬間重疊
                        //cats[personIndex].ForceCollapseToZero();
                        if (!cats[personIndex].IsCollapsed)
                            cats[personIndex].ForceCollapseToZero();

                    }
                }

                //cats[personIndex].UpdateHeadPosition(stablePercent);
                cats[personIndex].UpdateHeadPosition(finalPercent);
                cats[personIndex].UpdateAngle(slot);
            }
        }
        else if (spawnMode == CatSpawnMode.SlotDriven)
        {
            // 目前有人的 slot
            var activeSlotSet = new HashSet<int>(slotToPersons.Keys);

            // 移除「已經沒人」的 slot（先縮頭再刪）
            var removeSlots = new List<int>();
            foreach (var kv in slotToCat)
            {
                if (!activeSlotSet.Contains(kv.Key))
                {
                    RemoveCatWithCollapse(kv.Value);
                    cats.Remove(kv.Value); // 關鍵：同步移除
                    removeSlots.Add(kv.Key);
                }
            }

            for (int i = 0; i < removeSlots.Count; i++)
                slotToCat.Remove(removeSlots[i]);

            // 為「新出現的 slot」生成貓
            foreach (int slot in activeSlotSet)
            {
                if (!slotToCat.ContainsKey(slot))
                {
                    var newCat = Instantiate(catPrefab, catParent);
                    AssignRandomCatVideoSet(newCat);
                    slotToCat.Add(slot, newCat);
                }
            }

            // 更新每個 slot 對應的貓（不會再換）
            foreach (var kv in slotToCat)
            {
                int slot = kv.Key;
                var cat = kv.Value;

                var personsInSlot = slotToPersons[slot];

                // 取該 slot 中順位最前面的人
                //int allowedPerson = personsInSlot[0];
                //for (int k = 1; k < personsInSlot.Count; k++)
                //{
                //    if (personsInSlot[k] < allowedPerson)
                //        allowedPerson = personsInSlot[k];
                //}

                //int allowedPerson = personsInSlot[0];
                //float bestDist = float.MaxValue;

                //for (int k = 0; k < personsInSlot.Count; k++)
                //{
                //    int candidatePerson = personsInSlot[k];

                //    float rawAngle = angles[candidatePerson];
                //    float ext = Mathf.Repeat(rawAngle, 360f);
                //    float internalAngle = angleShift - ext;
                //    if (internalAngle < 0f) internalAngle += 360f;

                //    float dist = Mathf.Abs(Mathf.DeltaAngle(internalAngle, slot));

                //    if (dist < bestDist)
                //    {
                //        bestDist = dist;
                //        allowedPerson = candidatePerson;
                //    }
                //}

                int allowedPerson = GetClosestPersonToSlot(slot, personsInSlot, angles);

                float stablePercent = GetStableSkeletonPercent(
                    allowedPerson,
                    skeletonPercent[allowedPerson]
                );

                cat.UpdateAngle(slot);
                cat.UpdateHeadPosition(stablePercent);
            }
        }
    }
    private int GetClosestPersonToSlot(
    int slot,
    List<int> personsInSlot,
    List<float> angles
)
    {
        int bestPerson = personsInSlot[0];
        float bestDist = float.MaxValue;

        for (int k = 0; k < personsInSlot.Count; k++)
        {
            int candidatePerson = personsInSlot[k];

            float rawAngle = angles[candidatePerson];
            float ext = Mathf.Repeat(rawAngle, 360f);
            float internalAngle = angleShift - ext;
            if (internalAngle < 0f) internalAngle += 360f;

            float dist = Mathf.Abs(Mathf.DeltaAngle(internalAngle, slot));

            if (dist < bestDist)
            {
                bestDist = dist;
                bestPerson = candidatePerson;
            }
        }

        return bestPerson;
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
