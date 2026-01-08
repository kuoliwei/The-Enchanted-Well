using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 控制「井輸出畫面」的顯示校正腳本。
/// - 僅負責 RawImage 的顯示比例 / 縮放 / 位置
/// - 不影響世界座標、不影響 Camera、不影響井內邏輯
/// - InputField 行為完全模仿 CatManager 的 ApplyXXXFromInput 模式
/// </summary>
public class WellOutputImageController : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private RawImage rawImage;

    [Header("Design Base Size (1:1)")]
    [SerializeField] private float baseSize = 1000f;

    [Header("Aspect Scale Input")]
    [SerializeField] private InputField scaleInput;

    [Header("Aspect Offset Input")]
    [SerializeField] private InputField offsetXInput;
    [SerializeField] private InputField offsetYInput;

    [Header("Aspect Ratio Input")]
    [SerializeField] private InputField widthRatioInput;
    [SerializeField] private InputField heightRatioInput;

    // ===== Internal State（對齊 CatManager 的 internal 變數概念） =====
    private float currentScale;
    private PositionOffset currentOffset = new PositionOffset();
    private int currentWidthRatio;
    private int currentHeightRatio;

    #region Public API

    /// <summary>
    /// 套用整包系統設定（啟動或讀取 JSON 時呼叫）
    /// </summary>
    public void Apply(SystemConfigData config)
    {
        if (config == null || rawImage == null)
            return;

        // 1. 螢幕比例（RawImage 寬高）
        ApplyAspect();

        // 2. 井輸出縮放
        currentScale = config.wellCalibration.scale;
        ApplyScale();

        // 3. 井輸出位置補償
        currentOffset = config.wellCalibration.positionOffset;
        ApplyOffset();

        // 4. 同步顯示到 InputField
        SyncInputs();
    }

    #endregion

    #region Aspect / Size
    public void ApplyAspectFromInput()
    {
        if (!int.TryParse(widthRatioInput.text, out int w)) return;
        if (!int.TryParse(heightRatioInput.text, out int h)) return;
        if (w <= 0 || h <= 0) return;

        currentWidthRatio = w;
        currentHeightRatio = h;

        ApplyAspect();
    }
    private void ApplyAspect()
    {
        if (rawImage == null)
            return;

        RectTransform rt = rawImage.rectTransform;

        RectTransform parentRt = rt.parent as RectTransform;
        if (parentRt == null)
        {
            Debug.LogError("[WellOutput] RawImage parent is not RectTransform");
            return;
        }

        float parentWidth = parentRt.rect.width;
        float parentHeight = parentRt.rect.height;

        // 防呆：還沒輸入解析度就不算
        if (currentWidthRatio <= 0 || currentHeightRatio <= 0)
        {
            Debug.LogWarning("[WellOutput] Aspect ratio not set yet");
            return;
        }

        float squareSize;

        // ★ 完全依照「使用者輸入的第二銀幕解析度」判斷方向
        if (currentWidthRatio >= currentHeightRatio)
        {
            // 橫式銀幕（例如 1920x1080）→ 井貼高度
            squareSize = parentHeight;
        }
        else
        {
            // 直式銀幕（例如 1080x1920）→ 井貼寬度
            squareSize = parentWidth;
        }

        // 置中（不影響尺寸邏輯）
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);

        // ★ 用 sizeDelta 定義井的實際尺寸（1:1）
        rt.sizeDelta = new Vector2(squareSize, squareSize);

        Debug.Log(
            $"[WellOutput] ApplyAspect ratio({currentWidthRatio}x{currentHeightRatio}), " +
            $"parent({parentWidth}x{parentHeight}) → square {squareSize}"
        );
    }

    #endregion

    #region Scale

    public void ApplyScaleFromInput()
    {
        if (scaleInput == null)
            return;

        if (float.TryParse(scaleInput.text, out float parsed))
        {
            currentScale = parsed;
            ApplyScale();
            Debug.Log($"[WellOutput] scale updated: {currentScale}");
        }
    }

    private void ApplyScale()
    {
        rawImage.rectTransform.localScale = Vector3.one * currentScale;
    }

    #endregion

    #region Offset

    public void ApplyOffsetXFromInput()
    {
        if (offsetXInput == null)
            return;

        if (float.TryParse(offsetXInput.text, out float parsed))
        {
            currentOffset.x = parsed;
            ApplyOffset();
            Debug.Log($"[WellOutput] offset.x updated: {currentOffset.x}");
        }
    }

    public void ApplyOffsetYFromInput()
    {
        if (offsetYInput == null)
            return;

        if (float.TryParse(offsetYInput.text, out float parsed))
        {
            currentOffset.y = parsed;
            ApplyOffset();
            Debug.Log($"[WellOutput] offset.y updated: {currentOffset.y}");
        }
    }

    private void ApplyOffset()
    {
        rawImage.rectTransform.anchoredPosition =
            new Vector2(currentOffset.x, currentOffset.y);
    }

    #endregion

    #region UI Sync

    /// <summary>
    /// 將目前狀態同步回 InputField 顯示（避免啟動時是空的）
    /// </summary>
    private void SyncInputs()
    {
        if (scaleInput != null)
            scaleInput.text = currentScale.ToString("0.###");

        if (offsetXInput != null)
            offsetXInput.text = currentOffset.x.ToString("0.###");

        if (offsetYInput != null)
            offsetYInput.text = currentOffset.y.ToString("0.###");
    }

    #endregion
}