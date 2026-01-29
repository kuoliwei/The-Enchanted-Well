using PoseSocket;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
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

    private enum SlotHeadState
    {
        Idle,       // 尚未觸發
        HoldAt50,
        HoldAt100,
    }

    [Header("Cat Spawn Mode")]
    [SerializeField]
    private CatSpawnMode spawnMode = CatSpawnMode.SlotDriven;

    [Header("Data Source")]
    [SerializeField] private PoseDataReceiver poseReceiver;

    [Header("Prefab（包含貓控制器與影片播放器）")]
    [SerializeField] private CatMotionController catPrefab;

    [Header("貓生成的 UI Parent")]
    [SerializeField] private RectTransform catParent;

    [Header("貓花色影片組")]
    [SerializeField] private List<CatVideoSet> catVideoSets;

    [Header("資料中斷判定秒數")]
    [SerializeField] private float dataInterruptToCollapseSeconds = 1;

    [SerializeField] private float dataInterruptDestroyDelaySeconds = 5f;

    [Header("Slot Presence Settings")]
    [SerializeField]
    private float slotConfirmSeconds = 0.5f; // 某個 slot 連續存在多久才允許冒頭

    private readonly List<CatMotionController> cats = new List<CatMotionController>();
    //private float lastFrameTime = 0f;
    private float durationOfInterruption = 0f;
    //private Dictionary<int, int> slotToPersonIndex = new Dictionary<int, int>();
    // 尚未被使用的貓影片 index 池（不重複抽）
    private List<int> availableCatIndices = new List<int>();

    // 記錄每隻貓使用的是哪一個影片 index（用來回收）
    private Dictionary<CatMotionController, int> catToVideoIndex
        = new Dictionary<CatMotionController, int>();

    // SlotDriven 專用：slot → cat
    private Dictionary<int, CatMotionController> slotToCat
        = new Dictionary<int, CatMotionController>();

    // SlotDriven：slot → head state
    private Dictionary<int, SlotHeadStateData> slotHeadStates
        = new Dictionary<int, SlotHeadStateData>();

    [Header("Slot Head Timing")]
    [SerializeField] private InputField holdAt50DurationInput;
    [SerializeField] private float holdAt50Duration = 3;
    [SerializeField] private float waitFor100Duration = 1;

    [Header("Angle Snap Settings")]
    [SerializeField]
    private int[] snapAngles = { 115, 160, 205, 250 };

    [Header("Slot Range Settings")]
    [SerializeField]
    private float slotAngleRange = 20f; // ±多少度內才算進 slot

    [Header("Angle Shift")]
    [SerializeField] private InputField angleShiftInput;
    [SerializeField] private int angleShift = 45;
    private int internalShift => -angleShift;

    // ======== ★ 角度切換 threshold 設定 ========
    [Header("Angle Switch Threshold")]
    [SerializeField]
    private float angleSwitchThreshold = 10f;

    // ======== ★ 記錄每個人的上一個 slot ========
    private Dictionary<int, int> personLastSlot = new Dictionary<int, int>();

    // Slot → 最後一次被「任何人」佔用的時間
    private Dictionary<int, float> slotLastSeenTime = new Dictionary<int, float>();

    // Slot → 連續存在時間（在 Update 中用 Time.deltaTime 累積）
    private Dictionary<int, float> slotPresenceDuration = new Dictionary<int, float>();

    // slot → 是否鎖定（true = 不准移除）
    private Dictionary<int, bool> slotRemovalLock = new Dictionary<int, bool>();

    // slot → destroy coroutine
    private Dictionary<int, Coroutine> slotDestroyCoroutines
        = new Dictionary<int, Coroutine>();

    // ======== ★ skeletonPercent 變更門檻設定 ========
    [Header("Skeleton Percent Switch Threshold")]
    [SerializeField]
    private int skeletonPercentThresholdCount = 30;   // 連續幾筆才允許變更（可在 Inspector 調整）

    // SlotDriven：正在移除中的 slot（避免重複觸發與重生）
    private HashSet<int> slotRemoving = new HashSet<int>();

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

    [Header("Experience Counter")]
    [SerializeField]
    private ExperienceCounterBootstrapper experienceCounter;

    private void Awake()
    {
        //angleShiftInput.onValueChanged.AddListener(SetAngleShiftFromString);
        //holdAt50DurationInput.onValueChanged.AddListener(SetHoldAt50DurationFromString);
    }
    private void Start()
    {
        catToVideoIndex.Clear();
        ResetAvailableCats(); // 初始化可抽貓池
        if (poseReceiver != null)
            poseReceiver.OnSkeletonFrame += OnSkeletonFrame;
        else
            Debug.LogError("[CatManager] poseReceiver 未設定");

        Debug.Log($"[Init] slotRemoving count = {slotRemoving.Count}");
    }

    private void Update()
    {
        //// 資料中斷偵測
        //if (Time.time - lastFrameTime > timeoutSeconds)
        //{
        //    if (cats.Count > 0)
        //    {
        //        ClearAllCatsImmediate();
        //        Debug.Log("[CatManager] 資料中斷，立即清空所有貓");
        //    }
        //    SmoothRemoveAllCats();
        //}

        foreach (var kv in slotLastSeenTime)
        {
            int slot = kv.Key;
            float lastSeen = kv.Value;
            float missingTime = Time.time - lastSeen;

            if (missingTime > dataInterruptToCollapseSeconds)
            {
                // 一旦超過 timeout，表示這個 slot 已經「不算持續有人」，
                // 所以把連續存在時間歸零，等之後再重新累積
                slotPresenceDuration[slot] = 0f;

                Debug.Log(
    $"[SlotDebug-State] slot {slot} | " +
    $"hasCat={slotToCat.ContainsKey(slot)} | " +
    $"isRemoving={slotRemoving.Contains(slot)} | " +
    $"isLocked={(slotRemovalLock.ContainsKey(slot) ? slotRemovalLock[slot] : "N/A")}"
);

                // timeout → 解鎖（允許刪）
                if (!slotRemovalLock.ContainsKey(slot) || slotRemovalLock[slot] != false)
                {
                    slotRemovalLock[slot] = false;
                    Debug.Log($"[SlotLock] slot {slot} UNLOCK (missing {missingTime:F2}s)");
                }

                Debug.Log(
                    $"[SlotTimeout] slot {slot} NO PERSON for {missingTime:F2}s (>{dataInterruptToCollapseSeconds}s)"
                );

                // 關鍵：刪除期間鎖
                if (slotRemoving.Contains(slot))
                {
                    Debug.Log(
                        $"[SlotRemoveSkip] slot {slot} already removing, skip"
                    );
                }
                else
                {
                    if (slotToCat.TryGetValue(slot, out var cat))
                    {
                        Debug.Log($"[SlotRemove] slot {slot} START remove cat, cat.isReached0:{cat.isReached0}");
                        // 分流：未曾冒頭 → 立即刪除
                        if (cat.isReached0 && !cat.IsPoppedUpTriggered)
                        {
                            RemoveSlotCatImmediate(slot, cat);
                        }
                        else
                        {
                            RemoveSlotCatWithCollapse(slot, cat);
                        }
                    }
                }
            }
            else
            {
                // 尚未超過 timeout → 視為這個 slot 仍然「持續有人」
                // 在這裡用 Time.deltaTime 累積「連續存在時間」
                float duration = 0f;
                slotPresenceDuration.TryGetValue(slot, out duration);
                duration += Time.deltaTime;
                slotPresenceDuration[slot] = duration;

                // 尚未超過 timeout → 上鎖（禁止被清掉）
                if (!slotRemovalLock.ContainsKey(slot) || slotRemovalLock[slot] != true)
                {
                    slotRemovalLock[slot] = true;
                    Debug.Log(
                        $"[SlotLock] slot {slot} LOCK (missing {missingTime:F2}s)"
                    );
                }

                Debug.Log(
                    $"[SlotActive] slot {slot} last seen {missingTime:F2}s ago, presence={duration:F2}s"
                );
            }
        }
        durationOfInterruption += Time.deltaTime;
        // 資料中斷偵測
        if (durationOfInterruption > dataInterruptToCollapseSeconds)
        {
            Debug.Log($"durationOfInterruption:{durationOfInterruption}");
            if (spawnMode == CatSpawnMode.PersonDriven && cats.Count > 0)
            {
                SmoothRemoveAllCats();
                Debug.Log("[CatManager] 資料中斷，立即清空所有貓");
            }
            //if (spawnMode == CatSpawnMode.SlotDriven && slotToCat.Count > 0)
            //{
            //    SmoothRemoveAllCats();
            //    Debug.Log("[CatManager] 資料中斷，立即清空所有貓");
            //}
            durationOfInterruption = 0;
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

        int r = UnityEngine.Random.Range(0, availableCatIndices.Count);
        int catIndex = availableCatIndices[r];

        // 抽走就移除，確保不重複
        availableCatIndices.RemoveAt(r);

        return catIndex;
    }
    // 立即刪除，不等 frame 結束
    private void ClearAllCatsImmediate()
    {
        if (spawnMode == CatSpawnMode.PersonDriven)
        {
            for (int i = cats.Count - 1; i >= 0; i--)
            {
                DestroyImmediate(cats[i]);
            }
        }

        if (spawnMode == CatSpawnMode.SlotDriven)
        {
            foreach (var slot in slotToCat.Keys)
            {
                DestroyImmediate(slotToCat[slot]);
            }
        }
        if (spawnMode == CatSpawnMode.PersonDriven)
        {
            cats.Clear();
        }

        if (spawnMode == CatSpawnMode.SlotDriven)
        {
            slotToCat.Clear();
        }

        slotRemoving.Clear();
        personLastSlot.Clear(); // 避免舊角度殘留
        skeletonPercentCounters.Clear(); // 同步清空百分比狀態
        slotPresenceDuration.Clear();
        ResetAvailableCats(); // 重置可抽貓池
        slotHeadStates.Clear();
        Debug.Log($"[ClearAllCatsImmediate] slotRemoving cleared, count={slotRemoving.Count}");

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
        StartCoroutine(DestroyCatAfterDelay(cat, 1));
    }

    private IEnumerator DestroyCatAfterDelay(CatMotionController cat, float delay)
    {
        yield return new WaitUntil(() => cat.isReached100);
        yield return new WaitUntil(() => !cat.isForceUpdateHeadPosition);
        Debug.Log($"isReached100:{cat.isReached100},\n isForceUpdateHeadPosition:{cat.isForceUpdateHeadPosition}");

        cat.BeginSmoothCollapse();
        cat.hasPoppedAndCollapsed = true;
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
        if (spawnMode == CatSpawnMode.PersonDriven)
        {
            for (int i = cats.Count - 1; i >= 0; i--)
            {
                RemoveCatWithCollapse(cats[i]);
            }
        }

        if (spawnMode == CatSpawnMode.SlotDriven)
        {
            foreach (var slot in slotToCat.Keys)
            {
                RemoveCatWithCollapse(slotToCat[slot]);
            }
        }

        if (spawnMode == CatSpawnMode.PersonDriven)
        {
            cats.Clear();
        }

        if (spawnMode == CatSpawnMode.SlotDriven)
        {
            slotToCat.Clear();
        }
        slotRemoving.Clear();
        personLastSlot.Clear();
        skeletonPercentCounters.Clear();
        slotPresenceDuration.Clear();
        ResetAvailableCats();
        slotHeadStates.Clear();
    }
    private void RemoveSlotCatWithCollapse(int slot, CatMotionController cat)
    {
        // 已在移除中就不要重複觸發
        if (slotRemoving.Contains(slot))
            return;

        slotRemoving.Add(slot);
        var co = StartCoroutine(DestroySlotCatAfterDelay(slot, cat, dataInterruptDestroyDelaySeconds));
        slotDestroyCoroutines[slot] = co;
    }

    private IEnumerator DestroySlotCatAfterDelay(int slot, CatMotionController cat, float delay)
    {

        // 這段基本沿用你原本 DestroyCatAfterDelay 的流程
        yield return new WaitUntil(() => cat == null || cat.isReached100);

        if (cat == null)
        {
            // 貓已經不存在，收尾清理
            slotToCat.Remove(slot);
            slotHeadStates.Remove(slot);
            Debug.Log($"[SlotRemovingRemove] slot {slot} (cat already null)");
            slotRemoving.Remove(slot);
            yield break;
        }

        yield return new WaitUntil(() => !cat.isForceUpdateHeadPosition);

        cat.BeginSmoothCollapse();
        cat.hasPoppedAndCollapsed = true;

        yield return new WaitUntil(() => cat == null || cat.isReached0);

        yield return new WaitForSeconds(delay);

        if (cat != null)
        {
            RecycleCatVideo(cat);
            Destroy(cat.gameObject);
        }

        // 最重要：Destroy 之後才釋放 slot
        slotToCat.Remove(slot);
        slotHeadStates.Remove(slot);
        Debug.Log($"[SlotRemovingRemove] slot {slot} (after collapse)");
        slotRemoving.Remove(slot);

    }
    private void RemoveSlotCatImmediate(int slot, CatMotionController cat)
    {
        Debug.Log($"[SlotRemoveImmediate] slot {slot} remove cat immediately");

        // 與現行流程一致：避免重複刪除
        if (slotRemoving.Contains(slot))
            return;

        if (slotRemoving.Contains(slot))
        {
            Debug.Log($"[SlotRemovingAdd-SKIP] slot {slot} already in removing (Immediate)");
            return;
        }

        Debug.Log($"[SlotRemovingAdd] slot {slot} by RemoveSlotCatImmediate");

        slotRemoving.Add(slot);

        if (cat != null)
        {
            // 與 coroutine 流程一致：回收影片 index
            RecycleCatVideo(cat);

            // 立即刪除
            Destroy(cat.gameObject);
            Debug.Log($"[SlotRemovingRemove] slot {slot} by RemoveSlotCatImmediate");
            slotRemoving.Remove(slot);
        }

        // 與 DestroySlotCatAfterDelay 結尾完全一致
        slotToCat.Remove(slot);
        slotHeadStates.Remove(slot);
        //if (slotRemoving.Contains(slot))
        //{
        //    Debug.Log($"[SlotRemovingAdd-SKIP] slot {slot} already in removing (Immediate)");
        //    return;
        //}
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
        //if (frame != null)
        //{
        //    HandleSkeletonData(frame.angles, frame.skeletonPercent);
        //}

        if (frame == null) return;

        //lastFrameTime = Time.time;          // 全域斷訊只看「有沒有收到 frame」
        HandleSkeletonData(frame.angles, frame.skeletonPercent);
    }

    public void HandleSkeletonData(
        List<float> angles,
        List<float> skeletonPercent
    )
    {
        //lastFrameTime = Time.time;
        durationOfInterruption = 0;

        int personCount = angles.Count;
        if (personCount <= 0)
        {
            if (spawnMode == CatSpawnMode.PersonDriven)
            {
                SmoothRemoveAllCats();
            }
            //else if (spawnMode == CatSpawnMode.SlotDriven)
            //{
            //    // SlotDriven：斷訊/沒人時，不要 Clear slotToCat
            //    // 只觸發「延遲移除」，等 Destroy 完才釋放 slot
            //    var slots = new List<int>(slotToCat.Keys);
            //    for (int i = 0; i < slots.Count; i++)
            //    {
            //        int slot = slots[i];
            //        RemoveSlotCatWithCollapse(slot, slotToCat[slot]);
            //    }
            //}

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
            float internalAngle = internalShift - ext;
            if (internalAngle < 0f) internalAngle += 360f;

            //// 量化到 slot
            //int best = snapAngles[0];
            //float bestDist = Mathf.Abs(internalAngle - snapAngles[0]);

            //for (int s = 1; s < snapAngles.Length; s++)
            //{
            //    float d = Mathf.Abs(internalAngle - snapAngles[s]);
            //    if (d < bestDist)
            //    {
            //        bestDist = d;
            //        best = snapAngles[s];
            //    }
            //}

            // 量化到 slot（加上有效範圍限制）
            int best = -1;
            float bestDist = float.MaxValue;

            for (int s = 0; s < snapAngles.Length; s++)
            {
                int slotAngle = snapAngles[s];
                if (slotAngle == 360)
                    slotAngle = 0;

                // ★ 關鍵：slot 角度也要進 internalAngle 空間
                float slotInternal = internalShift - slotAngle;
                if (slotInternal < 0f) slotInternal += 360f;

                float dist = Mathf.Abs(Mathf.DeltaAngle(internalAngle, slotInternal));

                // ★ 超出 slotRange，直接略過
                if (dist > slotAngleRange)
                    continue;

                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = slotAngle;
                }
            }

            // ★ 如果沒有任何 slot 在範圍內，這個人直接略過
            if (best < 0)
            {
                // 這個人不佔任何 slot，也不產生貓
                continue;
            }


            if (best == 360)
                best = 0;

            // ======== ★ 新增：角度切換 threshold ========
            int finalSlot = best;

            if (personLastSlot.TryGetValue(i, out int lastSlot))
            {
                float lastSlotInternal = internalShift - lastSlot;
                if (lastSlotInternal < 0f) lastSlotInternal += 360f;

                float distFromLast = Mathf.Abs(Mathf.DeltaAngle(internalAngle, lastSlotInternal));

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
                float worldAngle = SlotToWorldAngle(slot);
                cats[personIndex].UpdateAngle(worldAngle);
            }
        }
        else if (spawnMode == CatSpawnMode.SlotDriven)
        {
            // 目前有人的 slot
            var activeSlotSet = new HashSet<int>(slotToPersons.Keys);

            float now = Time.time;

            // 更新：所有本幀有人的 slot
            foreach (int slot in activeSlotSet)
            {
                slotLastSeenTime[slot] = now;
            }


            // 移除「已經沒人」的 slot（先縮頭再刪）
            var removeSlots = new List<int>();

            //foreach (var kv in slotToCat)
            //{
            //    if (!activeSlotSet.Contains(kv.Key))
            //    {
            //        RemoveCatWithCollapse(kv.Value);
            //        removeSlots.Add(kv.Key);
            //    }
            //}

            //for (int i = 0; i < removeSlots.Count; i++)
            //{
            //    int slot = removeSlots[i];
            //    slotToCat.Remove(slot);
            //    slotHeadStates.Remove(slot); // 關鍵：同步清掉狀態
            //}

            //foreach (var kv in slotToCat)
            //{
            //    if (!activeSlotSet.Contains(kv.Key))
            //    {
            //        RemoveSlotCatWithCollapse(kv.Key, kv.Value);
            //    }
            //}

            // 為「新出現的 slot」生成貓
            foreach (int slot in activeSlotSet)
            {
                if (!slotToCat.ContainsKey(slot))
                {
                    var newCat = Instantiate(catPrefab, catParent);
                    AssignRandomCatVideoSet(newCat);
                    slotToCat.Add(slot, newCat);
                }
                else
                {
                    var cat = slotToCat[slot];

                    // 若此貓曾經冒出頭，且因人離開而縮頭，
                    // 當人回到該 slot 時，取消刪除並重設為等待冒頭狀態
                    if (cat.hasPoppedAndCollapsed)
                    {
                        Debug.Log($"[SlotCatReset] slot {slot} reset collapsed cat");

                        // 若已有啟動刪除協程，先取消
                        if (slotDestroyCoroutines.TryGetValue(slot, out var co))
                        {
                            StopCoroutine(co);
                            slotDestroyCoroutines.Remove(slot);
                        }

                        // 移除該 slot 的移除中狀態
                        slotRemoving.Remove(slot);

                        // 重設冒頭相關旗標，讓後續 slotConfirmSeconds 可再次觸發
                        cat.IsPoppedUpTriggered = false;
                        cat.hasPoppedAndCollapsed = false;
                    }
                }
            }

            // 更新每個 slot 對應的貓（不會再換）
            foreach (var kv in slotToCat)
            {
                int slot = kv.Key;
                var cat = kv.Value;

                if (!slotToPersons.TryGetValue(slot, out var personsInSlot))
                {
                    // 本幀這個 slot 沒有人（正常狀態）
                    // 直接跳過更新，不是錯誤
                    continue;
                }

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

                // === SlotDriven：不再直接使用 skeletonPercent 控制頭高度 ===

                // 取得 / 建立 slot 狀態
                if (!slotHeadStates.TryGetValue(slot, out var stateData))
                {
                    stateData = new SlotHeadStateData();
                    slotHeadStates[slot] = stateData;
                }

                //// 判斷是否觸發（只看是否為非 0）
                //float inputPercent = skeletonPercent[allowedPerson];
                ////Debug.Log($"inputPercent:{inputPercent}, cat.IsPoppedUpTriggered:{cat.IsPoppedUpTriggered}");
                //if(inputPercent > 0 && !cat.IsPoppedUpTriggered && !cat.IsPoppedUpTriggered)
                //{
                //    cat.IsPoppedUpTriggered = true;
                //    StartCoroutine(CatPoppingUp(cat));

                //}

                float inputPercent = skeletonPercent[allowedPerson];

                // 讀取這個 slot 已經連續存在多久（在 Update 裡累積的）
                float presenceDuration = 0f;
                slotPresenceDuration.TryGetValue(slot, out presenceDuration);

                // 只有「連續存在時間達標」才允許冒頭
                if (presenceDuration >= slotConfirmSeconds &&
                    inputPercent > 0f &&
                    !cat.IsPoppedUpTriggered)
                {
                    cat.IsPoppedUpTriggered = true;
                    StartCoroutine(CatPoppingUp(cat));
                }

                if (!stateData.triggered && inputPercent > 0f)
                {
                    stateData.triggered = true;
                    stateData.state = SlotHeadState.HoldAt50;
                    stateData.timer = 0f;
                }

                // STEP 2 暫時行為：
                // - 只要 slot 已被 trigger
                // - 頭就固定顯示在 50
                //float displayPercent = 0f;

                switch (stateData.state)
                {
                    case SlotHeadState.Idle:
                        //displayPercent = 0f;
                        break;

                    case SlotHeadState.HoldAt50:
                        //displayPercent = 50f;
                        stateData.timer += Time.deltaTime;

                        if (stateData.timer >= holdAt50Duration)
                        {
                            stateData.state = SlotHeadState.HoldAt100;
                            stateData.timer = 0f;
                        }
                        break;

                    case SlotHeadState.HoldAt100:
                        //displayPercent = 100f;
                        break;
                }

                float worldAngle = SlotToWorldAngle(slot);
                cat.UpdateAngle(worldAngle);
                //cat.UpdateHeadPosition(displayPercent);

            }

        }
    }
    private IEnumerator CatPoppingUp(CatMotionController cat)
    {
        Debug.Log($"CatPoppingUp執行");
        if (cat.IsPoppedUpTriggered)
        {
            cat.isForceUpdateHeadPosition = true;
            cat.forceUpdateHeadPosition(50);
            yield return new WaitForSeconds(holdAt50Duration);
            cat.forceUpdateHeadPosition(100);

            //yield return new WaitForSeconds(waitFor100Duration);
            yield return new WaitUntil(() => cat.isReached100);
            cat.isForceUpdateHeadPosition = false;
            Debug.Log($"isReached100:{cat.isReached100},\n isForceUpdateHeadPosition:{cat.isForceUpdateHeadPosition}");
            // ===== 體驗完成 → 人次 +1 =====
            if (experienceCounter != null)
            {
                var data = experienceCounter.LoadCounter();
                if (data != null)
                {
                    data.totalCount++;
                    data.lastUpdated = DateTimeOffset.Now.ToString("yyyy-MM-ddTHH:mm:sszzz");
                    experienceCounter.SaveCounter(data);

                    Debug.Log($"[ExperienceCounter] totalCount = {data.totalCount}");
                }
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
            float internalAngle = internalShift - ext;
            if (internalAngle < 0f) internalAngle += 360f;

            float slotInternal = internalShift - slot;
            if (slotInternal < 0f) slotInternal += 360f;

            float dist = Mathf.Abs(Mathf.DeltaAngle(internalAngle, slotInternal));


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
        //lastFrameTime = Time.time;

        durationOfInterruption = 0;
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
    private class SlotHeadStateData
    {
        public SlotHeadState state = SlotHeadState.Idle;
        public float timer = 0f;
        public bool triggered = false; // 是否已被非 0 skeletonPercent 觸發
    }
    public void SetAngleShiftFromString(string value)
    {
        if (int.TryParse(value, out int parsed))
        {
            angleShift = parsed;
        }
        Debug.Log($"angleShift:{angleShift}");
    }
    public void ApplyAngleShiftFromInput()
    {
        if (angleShiftInput == null)
            return;

        if (int.TryParse(angleShiftInput.text, out int parsed))
        {
            angleShift = parsed;
            Debug.Log($"angleShift updated: {angleShift}");
        }
    }
    public void SetHoldAt50DurationFromString(string value)
    {
        if (float.TryParse(value, out float parsed))
        {
            holdAt50Duration = parsed;
            Debug.Log($"secondsToRevealFullBody updated: {holdAt50Duration}");
        }
    }
    public void ApplyHoldAt50DurationFromInput()
    {
        if (holdAt50DurationInput == null)
            return;

        if (float.TryParse(holdAt50DurationInput.text, out float parsed))
        {
            holdAt50Duration = parsed;
            Debug.Log($"secondsToRevealFullBody updated: {holdAt50Duration}");
        }
    }
    private float SlotToWorldAngle(int slot)
    {
        // slot 是 internalAngle 空間
        float world = internalShift - slot;

        if (world < 0f)
            world += 360f;

        return world;
    }

}
