using Unity.VisualScripting;
using UnityEngine;

public class CatMotionController : MonoBehaviour
{
    [Header("Cat Objects")]
    [SerializeField] private RectTransform catRoot;    // 控制旋轉（angle）
    [SerializeField] private RectTransform catVideo;   // 控制位置（skeleton_percentage）

    [Header("Head Emergence Y Mapping")]
    public float yAt0;     // skeleton_percentage = 0
    public float yAt50;    // skeleton_percentage = 50
    public float yAt100;   // skeleton_percentage = 100

    [Header("Smooth Settings")]
    [SerializeField] private float smoothSpeed = 5f;

    private float targetY;
    private float currentY;

    private float targetPercent;

    private bool hasReceivedFirstData = false;
    [HideInInspector] public bool IsCollapsed { get; private set; }
    [HideInInspector] public bool IsPoppedUpTriggered = false;
    [HideInInspector] public bool isForceUpdateHeadPosition = false;
    [HideInInspector] public bool isReached100 = false;
    [HideInInspector] public bool isReached0 = false;

    //[Header("Angle Snap Settings")]
    //[SerializeField]
    //private int[] snapAngles = { 0, 60, 120, 180, 240, 300, 360 };

    // ------------------------------------------------------
    // 動補角度 → Unity 角度的轉換
    // ------------------------------------------------------
    //private float ConvertExternalAngleToUnity(float externalAngle)
    //{
    //    float ext = Mathf.Repeat(externalAngle, 360f);
    //    float internalAngle = 270f - ext;
    //    if (internalAngle < 0f) internalAngle += 360f;
    //    return internalAngle;
    //}

    void Start()
    {
        // 新貓生成 → 位置從 yAt0 開始（固定井底）
        currentY = yAt0;
        targetY = yAt0;

        Vector3 p = catVideo.localPosition;
        p.y = currentY;
        catVideo.localPosition = p;
    }

    void Update()
    {
        if (isForceUpdateHeadPosition)
        {
            UpdateHeadPosition(targetPercent);

            if (Mathf.Abs(yAt100 - currentY) < 0.0001f)
            {
                isReached100 = true;
            }

            Debug.Log($"isReached100:{isReached100},\n isForceUpdateHeadPosition:{isForceUpdateHeadPosition},\n currentY = {currentY},\n targetY = {targetY},\n currentY - targetY = {currentY - targetY},\n targetY - yAt100 < 0.0001f:{targetY - yAt100 < 0.0001f},\n targetY - currentY < 0.0001f:{targetY - currentY < 0.0001f}");
        }

        if (!isForceUpdateHeadPosition && IsCollapsed)
        {
            if (Mathf.Abs(yAt0 - currentY) < 0.0001f)
            {
                isReached0 = true;
            }
            Debug.Log($"isReached100:{isReached0},\n IsCollapsed:{IsCollapsed},\n isForceUpdateHeadPosition:{isForceUpdateHeadPosition}");
        }


        // 每幀平滑靠近目標
        currentY = Mathf.Lerp(currentY, targetY, Time.deltaTime * smoothSpeed);

        Vector3 p = catVideo.localPosition;
        p.y = currentY;
        catVideo.localPosition = p;
    }

    // ------------------------------------------------------
    // 更新角度（此段邏輯不可動，所以完全保留）
    // ------------------------------------------------------
    public void UpdateAngle(float angle)
    {
        if (catRoot == null)
            return;

        Vector3 e = catRoot.localEulerAngles;
        e.z = angle;
        catRoot.localEulerAngles = e;
    }


    // ------------------------------------------------------
    // 更新貓的頭部位置（僅改成設定 targetY + 平滑插值）
    // ------------------------------------------------------
    //public void UpdateHeadPosition(float percent)
    //{
    //    targetY = percent switch
    //    {
    //        <= 0 => yAt0,
    //        < 100 => Mathf.Lerp(yAt0, yAt100, percent / 100f),
    //        _ => yAt100
    //    };
    //}
    public void UpdateHeadPosition(float percent)
    {
        // 保證第一筆資料一定從 0 開始
        if (!hasReceivedFirstData)
        {
            hasReceivedFirstData = true;
            targetY = yAt0;
            IsCollapsed = true;   // 初始一定是縮頭
            return;
        }

        if (percent <= 0f)
        {
            targetY = yAt0;
        }
        else if (percent < 50f)
        {
            IsCollapsed = false; // 允許冒頭，解除縮頭
            // 0 → 50：井底到井口，移動較慢、可精調
            targetY = Mathf.Lerp(yAt0, yAt50, percent / 50f);
        }
        else if (percent < 100f)
        {
            IsCollapsed = false; // 允許冒頭，解除縮頭
            // 50 → 100：井口到完全冒出
            targetY = Mathf.Lerp(yAt50, yAt100, (percent - 50f) / 50f);
        }
        else
        {
            IsCollapsed = false; // 允許冒頭，解除縮頭
            targetY = yAt100;
        }
    }
    // ------------------------------------------------------
    // ★ 強制立即縮頭（無平滑，立刻回到 0）
    // ------------------------------------------------------
    public void ForceCollapseToZero()
    {
        hasReceivedFirstData = true; // 避免被視為「第一筆資料」
        targetY = yAt0;
        currentY = yAt0;

        Vector3 p = catVideo.localPosition;
        p.y = yAt0;
        catVideo.localPosition = p;

        IsCollapsed = true;
    }
    // ★ 用於人離場：只設定 target，不硬切
    public void BeginSmoothCollapse()
    {
        hasReceivedFirstData = true;
        targetY = yAt0;
        IsCollapsed = true;
    }
    public void forceUpdateHeadPosition(float percent)
    {
        Debug.Log($"forceUpdateHeadPosition執行, percent = {percent}");
        targetPercent = percent;
    }
}
