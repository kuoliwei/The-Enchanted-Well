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
        PersonDriven, // �¾���
        SlotDriven    // �s����
    }

    private enum SlotHeadState
    {
        Idle,       // �|��Ĳ�o
        HoldAt50,
        HoldAt100,
    }

    [Header("Cat Spawn Mode")]
    [SerializeField]
    private CatSpawnMode spawnMode = CatSpawnMode.SlotDriven;

    [Header("Data Source")]
    [SerializeField] private PoseDataReceiver poseReceiver;

    [Header("Prefab�]�]�t�߱���P�v�����񾹡^")]
    [SerializeField] private CatMotionController catPrefab;

    [Header("�ߥͦ��� UI Parent")]
    [SerializeField] private RectTransform catParent;

    [Header("�ߪ��v����")]
    [SerializeField] private List<CatVideoSet> catVideoSets;

    [Header("��Ƥ��_�P�w����")]
    [SerializeField] private InputField dataInterruptToCollapseSecondsInput;
    [SerializeField] private float dataInterruptToCollapseSeconds = 1;
    [SerializeField] private InputField dataInterruptDestroyDelaySecondsInput;
    [SerializeField] private float dataInterruptDestroyDelaySeconds = 5f;

    [Header("Slot Presence Settings")]
    [SerializeField]
    private float slotConfirmSeconds = 0.5f; // �Y�� slot �s��s�b�h�[�~���\�_�Y
    [SerializeField] private InputField slotConfirmSecondsInput;

    private readonly List<CatMotionController> cats = new List<CatMotionController>();
    //private float lastFrameTime = 0f;
    private float durationOfInterruption = 0f;
    //private Dictionary<int, int> slotToPersonIndex = new Dictionary<int, int>();
    // �|���Q�ϥΪ��߼v�� index ���]�����Ʃ�^
    private List<int> availableCatIndices = new List<int>();

    // �O���C���ߨϥΪ��O���@�Ӽv�� index�]�ΨӦ^���^
    private Dictionary<CatMotionController, int> catToVideoIndex
        = new Dictionary<CatMotionController, int>();

    // SlotDriven �M�ΡGslot �� cat
    private Dictionary<int, CatMotionController> slotToCat
        = new Dictionary<int, CatMotionController>();

    // SlotDriven�Gslot �� head state
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
    private float slotAngleRange = 20f; // �Ӧh�֫פ��~��i slot

    [Header("Angle Shift")]
    [SerializeField] private InputField angleShiftInput;
    [SerializeField] private int angleShift = 45;
    private int internalShift => -angleShift;

    // ======== �� ���פ��� threshold �]�w ========
    [Header("Angle Switch Threshold")]
    [SerializeField]
    private float angleSwitchThreshold = 10f;

    // ======== �� �O���C�ӤH���W�@�� slot ========
    private Dictionary<int, int> personLastSlot = new Dictionary<int, int>();

    // Slot �� �̫�@���Q�u����H�v���Ϊ��ɶ�
    private Dictionary<int, float> slotLastSeenTime = new Dictionary<int, float>();

    // Slot �� �s��s�b�ɶ��]�b Update ���� Time.deltaTime �ֿn�^
    private Dictionary<int, float> slotPresenceDuration = new Dictionary<int, float>();

    // slot �� �O�_��w�]true = ���㲾���^
    private Dictionary<int, bool> slotRemovalLock = new Dictionary<int, bool>();

    // slot �� destroy coroutine
    private Dictionary<int, Coroutine> slotDestroyCoroutines
        = new Dictionary<int, Coroutine>();

    // Global presence signal for external listeners (e.g. sound controller).
    // Mirrors the same slotConfirmSeconds-based confirmation used for cat trigger,
    // but aggregated across all slots: true while at least one slot is confirmed-present.
    public event Action OnAnyoneConfirmedPresent;
    public event Action OnEveryoneLeft;
    private bool anyoneConfirmedPresent = false;

    // Fires when all slots have been missing data for >= dataInterruptDestroyDelaySeconds.
    // Indicates permanent departure (all experience visitors have truly left the area).
    public event Action OnEveryonePermanentlyLeft;
    private bool anyonePermanentlyPresent = false;

    // ======== �� skeletonPercent �ܧ���e�]�w ========
    [Header("Skeleton Percent Switch Threshold")]
    [SerializeField]
    private int skeletonPercentThresholdCount = 30;   // �s��X���~���\�ܧ�]�i�b Inspector �վ�^

    // SlotDriven�G���b�������� slot�]�קK����Ĳ�o�P���͡^
    private HashSet<int> slotRemoving = new HashSet<int>();

    // �C�ӤH�� skeleton �ʤ���í�w���A
    private class SkeletonPercentCounter
    {
        public float currentValue = 0f;   // �ثe�w�ĥΪ�í�w�ƭȡ]0 / 50 / 100�^
        public float pendingValue = 0f;   // ���b���դ������s�ƭ�
        public int pendingCount = 0;      // pendingValue �w�s��X�{�X��
        public bool initialized = false;  // �O�_�w��l��
    }

    // personIndex �� SkeletonPercentCounter
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
        ResetAvailableCats(); // ��l�ƥi��ߦ�
        if (poseReceiver != null)
            poseReceiver.OnSkeletonFrame += OnSkeletonFrame;
        else
            Debug.LogError("[CatManager] poseReceiver ���]�w");

        Debug.Log($"[Init] slotRemoving count = {slotRemoving.Count}");
    }

    private void Update()
    {
        //// ��Ƥ��_����
        //if (Time.time - lastFrameTime > timeoutSeconds)
        //{
        //    if (cats.Count > 0)
        //    {
        //        ClearAllCatsImmediate();
        //        Debug.Log("[CatManager] ��Ƥ��_�A�ߧY�M�ũҦ���");
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
                // �@���W�L timeout�A���ܳo�� slot �w�g�u������򦳤H�v�A
                // �ҥH��s��s�b�ɶ��k�s�A������A���s�ֿn
                slotPresenceDuration[slot] = 0f;

                //                Debug.Log(
                //    $"[SlotDebug-State] slot {slot} | " +
                //    $"hasCat={slotToCat.ContainsKey(slot)} | " +
                //    $"isRemoving={slotRemoving.Contains(slot)} | " +
                //    $"isLocked={(slotRemovalLock.ContainsKey(slot) ? slotRemovalLock[slot] : "N/A")}"
                //);

                // timeout �� ����]���\�R�^
                if (!slotRemovalLock.ContainsKey(slot) || slotRemovalLock[slot] != false)
                {
                    slotRemovalLock[slot] = false;
                    Debug.Log($"[SlotLock] slot {slot} UNLOCK (missing {missingTime:F2}s)");
                }

                //Debug.Log(
                //    $"[SlotTimeout] slot {slot} NO PERSON for {missingTime:F2}s (>{dataInterruptToCollapseSeconds}s)"
                //);

                // ����G�R��������
                if (slotRemoving.Contains(slot))
                {
                    // Debug.Log(
                    //     $"[SlotRemoveSkip] slot {slot} already removing, skip"
                    // );
                }
                else
                {
                    if (slotToCat.TryGetValue(slot, out var cat))
                    {
                        Debug.Log($"[SlotRemove] slot {slot} START remove cat, cat.isReached0:{cat.isReached0}");
                        // ���y�G�����_�Y �� �ߧY�R��
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
                // �|���W�L timeout �� �����o�� slot ���M�u���򦳤H�v
                // �b�o�̥� Time.deltaTime �ֿn�u�s��s�b�ɶ��v
                float duration = 0f;
                slotPresenceDuration.TryGetValue(slot, out duration);
                duration += Time.deltaTime;
                slotPresenceDuration[slot] = duration;

                // �|���W�L timeout �� �W��]�T��Q�M���^
                if (!slotRemovalLock.ContainsKey(slot) || slotRemovalLock[slot] != true)
                {
                    slotRemovalLock[slot] = true;
                    Debug.Log($"[SlotLock] slot {slot} LOCK (missing {missingTime:F2}s)");
                }
                //Debug.Log($"[SlotActive] slot {slot} last seen {missingTime:F2}s ago, presence={duration:F2}s");
            }
        }

        // Check if all slots have been permanently missing (for >= dataInterruptDestroyDelaySeconds).
        bool allSlotsPermanentlyGone = slotLastSeenTime.Count > 0;

        foreach (var kv in slotLastSeenTime)
        {
            float missingTime = Time.time - kv.Value;
            if (missingTime < dataInterruptDestroyDelaySeconds)
            {
                allSlotsPermanentlyGone = false;
                break;
            }
        }
        if (allSlotsPermanentlyGone == true || anyonePermanentlyPresent == true)
        {
            Debug.Log($"[CatManager] After foreach: allSlotsPermanentlyGone={allSlotsPermanentlyGone}, anyonePermanentlyPresent={anyonePermanentlyPresent}");
        }

        // Fire permanent departure event on rising edge only.
        if (allSlotsPermanentlyGone != anyonePermanentlyPresent)
        {
            anyonePermanentlyPresent = allSlotsPermanentlyGone;
            if (anyonePermanentlyPresent)
            {
                OnEveryonePermanentlyLeft?.Invoke();
            }
        }

        durationOfInterruption += Time.deltaTime;
        // ��Ƥ��_����
        if (durationOfInterruption > dataInterruptToCollapseSeconds)
        {
            //Debug.Log($"durationOfInterruption:{durationOfInterruption}");
            if (spawnMode == CatSpawnMode.PersonDriven && cats.Count > 0)
            {
                SmoothRemoveAllCats();
                Debug.Log("[CatManager] ��Ƥ��_�A�ߧY�M�ũҦ���");
            }
            //if (spawnMode == CatSpawnMode.SlotDriven && slotToCat.Count > 0)
            //{
            //    SmoothRemoveAllCats();
            //    Debug.Log("[CatManager] ��Ƥ��_�A�ߧY�M�ũҦ���");
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
            Debug.LogWarning("[CatManager] �S���i�Ϊ��ߥi�H���t");
            return -1;
        }

        int r = UnityEngine.Random.Range(0, availableCatIndices.Count);
        int catIndex = availableCatIndices[r];

        // �⨫�N�����A�T�O������
        availableCatIndices.RemoveAt(r);

        return catIndex;
    }
    // �ߧY�R���A���� frame ����
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
        personLastSlot.Clear(); // �קK�¨��״ݯd
        skeletonPercentCounters.Clear(); // �P�B�M�Ŧʤ��񪬺A
        slotPresenceDuration.Clear();
        ResetAvailableCats(); // ���m�i��ߦ�
        slotHeadStates.Clear();
        Debug.Log($"[ClearAllCatsImmediate] slotRemoving cleared, count={slotRemoving.Count}");

    }

    private void EnsureCatCount(int count)
    {
        // �ͦ���������
        while (cats.Count < count)
        {
            var newCat = Instantiate(catPrefab, catParent);
            AssignRandomCatVideoSet(newCat);
            cats.Add(newCat);
        }

        // �R���h�l����
        //while (cats.Count > count)
        //{
        //    DestroyImmediate(cats[cats.Count - 1].gameObject);
        //    cats.RemoveAt(cats.Count - 1);
        //}
        while (cats.Count > count)
        {
            var lastCat = cats[cats.Count - 1];

            // �^���o���ߨϥΪ��v�� index
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
            // �^���v�� index�]����^
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
        // �w�b�������N���n����Ĳ�o
        if (slotRemoving.Contains(slot))
            return;

        slotRemoving.Add(slot);
        var co = StartCoroutine(DestroySlotCatAfterDelay(slot, cat, dataInterruptDestroyDelaySeconds));
        slotDestroyCoroutines[slot] = co;
    }

    private IEnumerator DestroySlotCatAfterDelay(int slot, CatMotionController cat, float delay)
    {

        // �o�q�򥻪u�ΧA�쥻 DestroyCatAfterDelay ���y�{
        yield return new WaitUntil(() => cat == null || cat.isReached100);

        if (cat == null)
        {
            // �ߤw�g���s�b�A�����M�z
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

        // �̭��n�GDestroy ����~���� slot
        slotToCat.Remove(slot);
        slotHeadStates.Remove(slot);
        Debug.Log($"[SlotRemovingRemove] slot {slot} (after collapse)");
        slotRemoving.Remove(slot);

    }
    private void RemoveSlotCatImmediate(int slot, CatMotionController cat)
    {
        Debug.Log($"[SlotRemoveImmediate] slot {slot} remove cat immediately");

        // �P�{��y�{�@�P�G�קK���ƧR��
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
            // �P coroutine �y�{�@�P�G�^���v�� index
            RecycleCatVideo(cat);

            // �ߧY�R��
            Destroy(cat.gameObject);
            Debug.Log($"[SlotRemovingRemove] slot {slot} by RemoveSlotCatImmediate");
            slotRemoving.Remove(slot);
        }

        // �P DestroySlotCatAfterDelay ���������@�P
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
            Debug.LogWarning("[CatManager] �䤣�� CatVideoPlayerController");
            return;
        }

        if (catVideoSets == null || catVideoSets.Count == 0)
        {
            Debug.LogWarning("[CatManager] catVideoSets �O�Ū�");
            return;
        }

        //int index = Random.Range(0, catVideoSets.Count);
        int index = DrawRandomUnusedCatIndex();
        if (index < 0)
            return;
        vp.videoClips = catVideoSets[index].clips;
        // �O���o���ߨϥΪ��v�� index�]����R���ɭn�^���^
        catToVideoIndex[cat] = index;
    }

    private void OnSkeletonFrame(SkeletonFrame frame)
    {
        //if (frame != null)
        //{
        //    HandleSkeletonData(frame.angles, frame.skeletonPercent);
        //}

        if (frame == null) return;

        //lastFrameTime = Time.time;          // �����_�T�u�ݡu���S������ frame�v
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
            //    // SlotDriven�G�_�T/�S�H�ɡA���n Clear slotToCat
            //    // �uĲ�o�u���𲾰��v�A�� Destroy ���~���� slot
            //    var slots = new List<int>(slotToCat.Keys);
            //    for (int i = 0; i < slots.Count; i++)
            //    {
            //        int slot = slots[i];
            //        RemoveSlotCatWithCollapse(slot, slotToCat[slot]);
            //    }
            //}

            return;
        }
        // �����w������ personIndex�]�קK�ݯd���A�^
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

        // �̶���B�z�C�ӤH
        for (int i = 0; i < personCount; i++)
        {
            float rawAngle = angles[i];

            // �y���ഫ�]��˫O�d�^
            float ext = Mathf.Repeat(rawAngle, 360f);
            float internalAngle = internalShift - ext;
            if (internalAngle < 0f) internalAngle += 360f;

            //// �q�ƨ� slot
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

            // �q�ƨ� slot�]�[�W���Ľd�򭭨�^
            int best = -1;
            float bestDist = float.MaxValue;

            for (int s = 0; s < snapAngles.Length; s++)
            {
                int slotAngle = snapAngles[s];
                if (slotAngle == 360)
                    slotAngle = 0;

                // �� ����Gslot ���פ]�n�i internalAngle �Ŷ�
                float slotInternal = internalShift - slotAngle;
                if (slotInternal < 0f) slotInternal += 360f;

                float dist = Mathf.Abs(Mathf.DeltaAngle(internalAngle, slotInternal));

                // �� �W�X slotRange�A�������L
                if (dist > slotAngleRange)
                    continue;

                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = slotAngle;
                }
            }

            // �� �p�G�S������ slot �b�d�򤺡A�o�ӤH�������L
            if (best < 0)
            {
                // �o�ӤH�������� slot�A�]�����Ϳ�
                continue;
            }


            if (best == 360)
                best = 0;

            // ======== �� �s�W�G���פ��� threshold ========
            int finalSlot = best;

            if (personLastSlot.TryGetValue(i, out int lastSlot))
            {
                float lastSlotInternal = internalShift - lastSlot;
                if (lastSlotInternal < 0f) lastSlotInternal += 360f;

                float distFromLast = Mathf.Abs(Mathf.DeltaAngle(internalAngle, lastSlotInternal));

                if (distFromLast < angleSwitchThreshold)
                {
                    // �٦b�������e�� �� �u���� slot
                    finalSlot = lastSlot;
                }
                else
                {
                    // �u���� slot �� ���m skeletonPercent
                    skeletonPercentCounters.Remove(i);
                }
            }

            personLastSlot[i] = finalSlot;
            best = finalSlot;

            // �O�� slot �� persons
            if (!slotToPersons.TryGetValue(finalSlot, out var list))
            {
                list = new List<int>();
                slotToPersons[finalSlot] = list;
            }
            list.Add(i);
            // ============================================

            // �P�@���ץu���\�@���ߡ]��˫O�d�^
            //if (slotToPersonIndex.ContainsKey(best))
            //    continue;

            //slotToPersonIndex.Add(best, i);
            //slotToPersonIndex[best] = i;
        }

        // �T�O�߼ƶq = ���� slot ��
        //EnsureCatCount(slotToPersonIndex.Count);
        //EnsureCatCount(personCount);

        //int catIndex = 0;
        //foreach (var kvp in slotToPersonIndex)
        //{
        //    int personIndex = kvp.Value;

        //    // ���g�L���e����A���o�uí�w��v���ʤ���
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

                // �p�G�P�@�� slot ���h�ӤH�A�u���\����Ĥ@���H�_�Y
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
                        // �ߨ�j���Y�Y�A�קK�������|
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
            // �ثe���H�� slot
            var activeSlotSet = new HashSet<int>(slotToPersons.Keys);

            float now = Time.time;

            // ��s�G�Ҧ����V���H�� slot
            foreach (int slot in activeSlotSet)
            {
                slotLastSeenTime[slot] = now;
            }


            // �����u�w�g�S�H�v�� slot�]���Y�Y�A�R�^
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
            //    slotHeadStates.Remove(slot); // ����G�P�B�M�����A
            //}

            //foreach (var kv in slotToCat)
            //{
            //    if (!activeSlotSet.Contains(kv.Key))
            //    {
            //        RemoveSlotCatWithCollapse(kv.Key, kv.Value);
            //    }
            //}

            // ���u�s�X�{�� slot�v�ͦ���
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

                    // �Y���ߴ��g�_�X�Y�A�B�]�H���}���Y�Y�A
                    // ���H�^��� slot �ɡA�����R���í��]�����ݫ_�Y���A
                    if (cat.hasPoppedAndCollapsed)
                    {
                        Debug.Log($"[SlotCatReset] slot {slot} reset collapsed cat");

                        // �Y�w���ҰʧR����{�A������
                        if (slotDestroyCoroutines.TryGetValue(slot, out var co))
                        {
                            StopCoroutine(co);
                            slotDestroyCoroutines.Remove(slot);
                        }

                        // ������ slot �����������A
                        slotRemoving.Remove(slot);

                        // ���]�_�Y�����X�СA������ slotConfirmSeconds �i�A��Ĳ�o
                        cat.IsPoppedUpTriggered = false;
                        cat.hasPoppedAndCollapsed = false;
                    }
                }
            }

            // ��s�C�� slot �������ߡ]���|�A���^
            bool anyConfirmedThisFrame = false;
            foreach (var kv in slotToCat)
            {
                int slot = kv.Key;
                var cat = kv.Value;

                if (!slotToPersons.TryGetValue(slot, out var personsInSlot))
                {
                    // ���V�o�� slot �S���H�]���`���A�^
                    // �������L��s�A���O���~
                    continue;
                }

                // ���� slot ������̫e�����H
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

                // === SlotDriven�G���A�����ϥ� skeletonPercent �����Y���� ===

                // ���o / �إ� slot ���A
                if (!slotHeadStates.TryGetValue(slot, out var stateData))
                {
                    stateData = new SlotHeadStateData();
                    slotHeadStates[slot] = stateData;
                }

                //// �P�_�O�_Ĳ�o�]�u�ݬO�_���D 0�^
                //float inputPercent = skeletonPercent[allowedPerson];
                ////Debug.Log($"inputPercent:{inputPercent}, cat.IsPoppedUpTriggered:{cat.IsPoppedUpTriggered}");
                //if(inputPercent > 0 && !cat.IsPoppedUpTriggered && !cat.IsPoppedUpTriggered)
                //{
                //    cat.IsPoppedUpTriggered = true;
                //    StartCoroutine(CatPoppingUp(cat));

                //}

                float inputPercent = skeletonPercent[allowedPerson];

                // Ū���o�� slot �w�g�s��s�b�h�[�]�b Update �ֿ̲n���^
                float presenceDuration = 0f;
                slotPresenceDuration.TryGetValue(slot, out presenceDuration);

                // �u���u�s��s�b�ɶ��F�Сv�~���\�_�Y
                if (presenceDuration >= slotConfirmSeconds &&
                    inputPercent > 0f &&
                    !cat.IsPoppedUpTriggered)
                {
                    cat.IsPoppedUpTriggered = true;
                    StartCoroutine(CatPoppingUp(cat));
                }

                // Same confirmation rule as above, but used purely to build the
                // global "anyone confirmed present" aggregate (no side effects here).
                if (presenceDuration >= slotConfirmSeconds && inputPercent > 0f)
                {
                    anyConfirmedThisFrame = true;
                }

                if (!stateData.triggered && inputPercent > 0f)
                {
                    stateData.triggered = true;
                    stateData.state = SlotHeadState.HoldAt50;
                    stateData.timer = 0f;
                }

                // STEP 2 �Ȯɦ欰�G
                // - �u�n slot �w�Q trigger
                // - �Y�N�T�w��ܦb 50
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

            // Fire global presence change events on rising/falling edge only.
            if (anyConfirmedThisFrame != anyoneConfirmedPresent)
            {
                anyoneConfirmedPresent = anyConfirmedThisFrame;
                if (anyoneConfirmedPresent)
                {
                    OnAnyoneConfirmedPresent?.Invoke();
                }
                else
                {
                    OnEveryoneLeft?.Invoke();
                }
            }

        }
    }
    private IEnumerator CatPoppingUp(CatMotionController cat)
    {
        Debug.Log($"CatPoppingUp����");
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
            // ===== ���秹�� �� �H�� +1 =====
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
    /// �ھڡu�s��X�{���ơv�M�w�O�_���\���� skeletonPercent�C
    /// - �C�� personIndex �U�ۦ��W�ߪ� counter�C
    /// - �u���� newValue �s��X�{�F�� skeletonPercentThresholdCount ���ɡA
    ///   �~�|�u������ currentValue�C
    /// - �^�ǭ� = �ثe���\�ϥΪ�í�w�ƭȡC
    /// </summary>
    private float GetStableSkeletonPercent(int personIndex, float newValue)
    {
        if (!skeletonPercentCounters.TryGetValue(personIndex, out var counter))
        {
            // �Ĥ@���ݨ�o�ӤH�G�j��q 0 �}�l
            counter = new SkeletonPercentCounter
            {
                currentValue = 0f,
                pendingValue = newValue,
                pendingCount = 1,
                initialized = true
            };

            skeletonPercentCounters[personIndex] = counter;
            return counter.currentValue; // �@�}�l�@�w�^�� 0
        }

        // �p�G�s�ȸ�ثeí�w�Ȥ@�� �� �L�������A���m pending �p��
        if (Mathf.Approximately(newValue, counter.currentValue))
        {
            counter.pendingValue = newValue;
            counter.pendingCount = 0;
            return counter.currentValue;
        }

        // �s�ȻP�ثeí�w�Ȥ��P�G�ˬd pending ���A
        if (!Mathf.Approximately(newValue, counter.pendingValue))
        {
            // ���F�@�ӷs���Կ�ȡA���s�p��
            counter.pendingValue = newValue;
            counter.pendingCount = 1;
        }
        else
        {
            // �Կ�ȻP�W���ۦP�A�W�[�s�򦸼�
            counter.pendingCount++;
        }

        // �|���F����e �� �������A�����쥻�� currentValue
        if (counter.pendingCount < skeletonPercentThresholdCount)
            return counter.currentValue;

        // �F����e �� ��������
        counter.currentValue = counter.pendingValue;
        counter.pendingCount = 0; // ���m�p�ơA���ݤU�@���ܤ�

        return counter.currentValue;
    }
    private class SlotHeadStateData
    {
        public SlotHeadState state = SlotHeadState.Idle;
        public float timer = 0f;
        public bool triggered = false; // �O�_�w�Q�D 0 skeletonPercent Ĳ�o
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

    public void ApplySlotConfirmSecondsFromInput()
    {
        if (slotConfirmSecondsInput == null)
            return;

        if (float.TryParse(slotConfirmSecondsInput.text, out float parsed))
        {
            slotConfirmSeconds = parsed;
            Debug.Log($"secondsToConfirmPeopleShowUp updated: {slotConfirmSeconds}");
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
    public void SetDataInterruptToCollapseSeconds(string value)
    {
        if (float.TryParse(value, out float parsed))
        {
            dataInterruptToCollapseSeconds = parsed;
            Debug.Log($"SecondsPersonLeavesTemporarily updated: {dataInterruptToCollapseSeconds}");
        }
    }
    public void ApplyDataInterruptToCollapseSeconds()
    {
        if (dataInterruptToCollapseSecondsInput == null)
            return;

        if (float.TryParse(dataInterruptToCollapseSecondsInput.text, out float parsed))
        {
            dataInterruptToCollapseSeconds = parsed;
            Debug.Log($"SecondsPersonLeavesTemporarily updated: {dataInterruptToCollapseSeconds}");
        }
    }
    public void SetDataInterruptDestroyDelaySeconds(string value)
    {
        if (float.TryParse(value, out float parsed))
        {
            dataInterruptDestroyDelaySeconds = parsed;
            Debug.Log($"SecondsPersonLeavesPermanently updated: {dataInterruptDestroyDelaySeconds}");
        }
    }
    public void ApplyDataInterruptDestroyDelaySeconds()
    {
        if (dataInterruptDestroyDelaySecondsInput == null)
            return;

        if (float.TryParse(dataInterruptDestroyDelaySecondsInput.text, out float parsed))
        {
            dataInterruptDestroyDelaySeconds = parsed;
            Debug.Log($"SecondsPersonLeavesPermanently updated: {dataInterruptDestroyDelaySeconds}");
        }
    }

    private float SlotToWorldAngle(int slot)
    {
        // slot �O internalAngle �Ŷ�
        float world = internalShift - slot;

        if (world < 0f)
            world += 360f;

        return world;
    }

}
