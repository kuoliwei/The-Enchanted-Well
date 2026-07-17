using UnityEngine;

[System.Serializable]
public class WaveBlurPreset
{
    public string name;
    public float blurRadius;
    public float opaquePoint;
    public float transparentPoint;
    public float maxAlphaValue;
    public int waveCountX;
    public int waveCountY;
    public bool waveAnimate;
    public float[] waveFrequenciesX;
    public float[] waveSpeedsX;
    public float[] waveStrengthsX;
    public float[] ampVarSpeedsX;
    public float[] waveFrequenciesY;
    public float[] waveSpeedsY;
    public float[] waveStrengthsY;
    public float[] ampVarSpeedsY;
    public bool enablePulse;
    public float pulseMultiplier;
    public float centerBrightnessMultiplier;
    public float brightnessRange;
    public float brightnessOffset;
    public float brightnessFalloff;
    public float fadeStartY;
    public float fadeEndAlpha;
}

[System.Serializable]
public class WaveBlurPresetList
{
    public WaveBlurPreset[] presets;
}

[ExecuteAlways]
public class BlackKeyWaveBlurController : MonoBehaviour
{
    private Material targetMaterial;

    // Called by CatVideoPlayerController after it instantiates the material.
    public void SetMaterial(Material mat) => targetMaterial = mat;

    [HideInInspector][SerializeField] private int selectedPresetIndex = 0;

    [Header("Blur")]
    [Range(0f, 20f)] public float blurRadius;

    [Header("Black Key Removal")]
    [Range(0f, 0.02f)] public float opaquePoint;
    [Range(0f, 0.005f)] public float transparentPoint;
    [Range(0f, 1f)] public float maxAlphaValue;

    [Header("Wave")]
    [Range(1, 8)] public int waveCountX;
    [Range(1, 8)] public int waveCountY;
    public bool waveAnimate;

    [Header("X Direction Waves  (Frequencies / Speeds / Strengths / PulseSpeeds)")]
    public float[] waveFrequenciesX;
    public float[] waveSpeedsX;
    public float[] waveStrengthsX;
    public float[] ampVarSpeedsX;

    [Header("Y Direction Waves  (Frequencies / Speeds / Strengths / PulseSpeeds)")]
    public float[] waveFrequenciesY;
    public float[] waveSpeedsY;
    public float[] waveStrengthsY;
    public float[] ampVarSpeedsY;

    [Header("Amplitude Pulse")]
    public bool enablePulse;
    [Range(0f, 3f)] public float pulseMultiplier;

    [Header("Gaussian Brightness")]
    [Range(1f, 10f)] public float centerBrightnessMultiplier;
    [Range(0.05f, 0.5f)] public float brightnessRange;
    [Range(-0.5f, 0.5f)] public float brightnessOffset;
    [Range(0.05f, 0.9f)] public float brightnessFalloff;

    [Header("Y Fade  (FadeStartY=1 = disabled)")]
    [Range(0f, 1f)] public float fadeStartY;
    [Range(0f, 1f)] public float fadeEndAlpha;

    // Cached property IDs
    private static readonly int BlurRadiusID = Shader.PropertyToID("_BlurRadius");
    private static readonly int OpaquePointID = Shader.PropertyToID("_OpaquePoint");
    private static readonly int TransparentPointID = Shader.PropertyToID("_TransparentPoint");
    private static readonly int MaxAlphaValueID = Shader.PropertyToID("_MaxAlphaValue");
    private static readonly int WaveCountXID = Shader.PropertyToID("_WaveCountX");
    private static readonly int WaveCountYID = Shader.PropertyToID("_WaveCountY");
    private static readonly int WaveAnimateID = Shader.PropertyToID("_WaveAnimate");
    private static readonly int WaveFreqXID = Shader.PropertyToID("_WaveFrequenciesX");
    private static readonly int WaveSpeedXID = Shader.PropertyToID("_WaveSpeedsX");
    private static readonly int WaveStrXID = Shader.PropertyToID("_WaveStrengthsX");
    private static readonly int WaveFreqYID = Shader.PropertyToID("_WaveFrequenciesY");
    private static readonly int WaveSpeedYID = Shader.PropertyToID("_WaveSpeedsY");
    private static readonly int WaveStrYID = Shader.PropertyToID("_WaveStrengthsY");
    private static readonly int CenterBrightnessID = Shader.PropertyToID("_CenterBrightnessMultiplier");
    private static readonly int BrightnessRangeID = Shader.PropertyToID("_BrightnessRange");
    private static readonly int BrightnessOffsetID = Shader.PropertyToID("_BrightnessOffset");
    private static readonly int BrightnessFalloffID = Shader.PropertyToID("_BrightnessFalloff");
    private static readonly int FadeStartYID = Shader.PropertyToID("_FadeStartY");
    private static readonly int FadeEndAlphaID = Shader.PropertyToID("_FadeEndAlpha");

    private float[] currentWaveStrengthsX = new float[8];
    private float[] currentWaveStrengthsY = new float[8];

    private WaveBlurPresetList presetList;

    // Called by Unity Editor when the component is first added or Reset is selected.
    private void Reset()
    {
        LoadPresets();
        if (presetList?.presets?.Length > 0)
            ApplyPreset(0);
    }

    private void OnEnable()
    {
        if (presetList == null)
            LoadPresets();
    }

    private void LoadPresets()
    {
        var asset = Resources.Load<TextAsset>("BlackKeyWaveBlurPresets");
        if (asset != null)
            presetList = JsonUtility.FromJson<WaveBlurPresetList>(asset.text);
        else
            Debug.LogWarning("[BlackKeyWaveBlurController] BlackKeyWaveBlurPresets.json not found in Resources folder.");
    }

    public string[] GetPresetNames()
    {
        if (presetList == null) LoadPresets();
        if (presetList?.presets == null) return null;
        return System.Array.ConvertAll(presetList.presets, p => p.name);
    }

    public void ApplyPreset(int index)
    {
        if (presetList == null) LoadPresets();
        if (presetList?.presets == null || index < 0 || index >= presetList.presets.Length) return;

        var p = presetList.presets[index];
        selectedPresetIndex = index;

        blurRadius = p.blurRadius;
        opaquePoint = p.opaquePoint;
        transparentPoint = p.transparentPoint;
        maxAlphaValue = p.maxAlphaValue;
        waveCountX = p.waveCountX;
        waveCountY = p.waveCountY;
        waveAnimate = p.waveAnimate;

        CopyArray(p.waveFrequenciesX, ref waveFrequenciesX);
        CopyArray(p.waveSpeedsX, ref waveSpeedsX);
        CopyArray(p.waveStrengthsX, ref waveStrengthsX);
        CopyArray(p.ampVarSpeedsX, ref ampVarSpeedsX);
        CopyArray(p.waveFrequenciesY, ref waveFrequenciesY);
        CopyArray(p.waveSpeedsY, ref waveSpeedsY);
        CopyArray(p.waveStrengthsY, ref waveStrengthsY);
        CopyArray(p.ampVarSpeedsY, ref ampVarSpeedsY);

        enablePulse = p.enablePulse;
        pulseMultiplier = p.pulseMultiplier;
        centerBrightnessMultiplier = p.centerBrightnessMultiplier;
        brightnessRange = p.brightnessRange;
        brightnessOffset = p.brightnessOffset;
        brightnessFalloff = p.brightnessFalloff;
        fadeStartY = p.fadeStartY;
        fadeEndAlpha = p.fadeEndAlpha;

        if (targetMaterial != null)
        {
            System.Array.Copy(waveStrengthsX, currentWaveStrengthsX, 8);
            System.Array.Copy(waveStrengthsY, currentWaveStrengthsY, 8);
            PushToMaterial();
        }
    }

    private static void CopyArray(float[] src, ref float[] dst)
    {
        if (src == null) return;
        if (dst == null || dst.Length != src.Length)
            dst = new float[src.Length];
        System.Array.Copy(src, dst, src.Length);
    }

    private void Update()
    {
        if (targetMaterial == null) return;

        if (enablePulse)
            UpdateAmplitudePulse();
        else
        {
            System.Array.Copy(waveStrengthsX, currentWaveStrengthsX, 8);
            System.Array.Copy(waveStrengthsY, currentWaveStrengthsY, 8);
        }

        PushToMaterial();
    }

    // Called automatically by Unity whenever any Inspector value changes (Edit Mode).
    private void OnValidate()
    {
        if (targetMaterial == null) return;

        // Pulse uses Time.time which is unavailable in Edit Mode; use raw strengths instead.
        System.Array.Copy(waveStrengthsX, currentWaveStrengthsX, 8);
        System.Array.Copy(waveStrengthsY, currentWaveStrengthsY, 8);

        PushToMaterial();

#if UNITY_EDITOR
        UnityEditor.SceneView.RepaintAll();
#endif
    }

    private void PushToMaterial()
    {
        targetMaterial.SetFloat(BlurRadiusID, blurRadius);

        targetMaterial.SetFloat(OpaquePointID, opaquePoint);
        targetMaterial.SetFloat(TransparentPointID, transparentPoint);
        targetMaterial.SetFloat(MaxAlphaValueID, maxAlphaValue);

        targetMaterial.SetInt(WaveCountXID, waveCountX);
        targetMaterial.SetInt(WaveCountYID, waveCountY);
        targetMaterial.SetFloat(WaveAnimateID, waveAnimate ? 1f : 0f);
        targetMaterial.SetFloatArray(WaveFreqXID, waveFrequenciesX);
        targetMaterial.SetFloatArray(WaveSpeedXID, waveSpeedsX);
        targetMaterial.SetFloatArray(WaveStrXID, currentWaveStrengthsX);
        targetMaterial.SetFloatArray(WaveFreqYID, waveFrequenciesY);
        targetMaterial.SetFloatArray(WaveSpeedYID, waveSpeedsY);
        targetMaterial.SetFloatArray(WaveStrYID, currentWaveStrengthsY);

        targetMaterial.SetFloat(CenterBrightnessID, centerBrightnessMultiplier);
        targetMaterial.SetFloat(BrightnessRangeID, brightnessRange);
        targetMaterial.SetFloat(BrightnessOffsetID, brightnessOffset);
        targetMaterial.SetFloat(BrightnessFalloffID, brightnessFalloff);

        targetMaterial.SetFloat(FadeStartYID, fadeStartY);
        targetMaterial.SetFloat(FadeEndAlphaID, fadeEndAlpha);
    }

    private void UpdateAmplitudePulse()
    {
        for (int i = 0; i < waveCountX; i++)
        {
            float s = Mathf.Sin(2f * Mathf.PI * Time.time * ampVarSpeedsX[i]);
            currentWaveStrengthsX[i] = Mathf.Abs(s) * pulseMultiplier * waveStrengthsX[i];
        }
        for (int j = 0; j < waveCountY; j++)
        {
            float s = Mathf.Sin(2f * Mathf.PI * Time.time * ampVarSpeedsY[j]);
            currentWaveStrengthsY[j] = Mathf.Abs(s) * pulseMultiplier * waveStrengthsY[j];
        }
    }
}
