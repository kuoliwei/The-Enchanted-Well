using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class RadialWaveBlurPreset
{
    public string name;
    public float blurRadius;
    public int waveCountRadial;
    public int waveCountAngular;
    public bool waveAnimate;
    public float[] waveFrequenciesRadial;
    public float[] waveSpeedsRadial;
    public float[] waveStrengthsRadial;
    public float[] ampVarSpeedsRadial;
    public float[] waveFrequenciesAngular;
    public float[] waveSpeedsAngular;
    public float[] waveStrengthsAngular;
    public float[] ampVarSpeedsAngular;
    public bool enablePulse;
    public float pulseMultiplier;
    public float centerBrightnessMultiplier;
    public float brightnessRange;
    public float brightnessOffset;
    public float brightnessFalloff;
    public float fadeStartRadius;
    public float fadeEndAlpha;
}

[System.Serializable]
public class RadialWaveBlurPresetList
{
    public RadialWaveBlurPreset[] presets;
}

[ExecuteAlways]
public class RadialWaveBlurController : MonoBehaviour
{
    private Material targetMaterial;

    // Called by CatVideoPlayerController after it instantiates the material.
    public void SetMaterial(Material mat) => targetMaterial = mat;

    [HideInInspector][SerializeField] private int selectedPresetIndex = 0;

    [Header("Blur")]
    [Range(0f, 20f)] public float blurRadius;

    [Header("Radial Wave")]
    [Range(0, 8)] public int waveCountRadial;
    [Range(0, 8)] public int waveCountAngular;
    public bool waveAnimate;

    [Header("Radial Direction Waves  (Frequencies / Speeds / Strengths / PulseSpeeds)")]
    public float[] waveFrequenciesRadial;
    public float[] waveSpeedsRadial;
    public float[] waveStrengthsRadial;
    public float[] ampVarSpeedsRadial;

    [Header("Angular Direction Waves  (Frequencies / Speeds / Strengths / PulseSpeeds)")]
    public float[] waveFrequenciesAngular;
    public float[] waveSpeedsAngular;
    public float[] waveStrengthsAngular;
    public float[] ampVarSpeedsAngular;

    [Header("Amplitude Pulse")]
    public bool enablePulse;
    [Range(0f, 3f)] public float pulseMultiplier;

    [Header("Gaussian Brightness")]
    [Range(1f, 10f)] public float centerBrightnessMultiplier;
    [Range(0.05f, 0.5f)] public float brightnessRange;
    [Range(-0.5f, 0.5f)] public float brightnessOffset;
    [Range(0.05f, 0.9f)] public float brightnessFalloff;

    [Header("Radial Fade  (FadeStartRadius=1 = disabled)")]
    [Range(0f, 1f)] public float fadeStartRadius;
    [Range(0f, 1f)] public float fadeEndAlpha;

    // Cached property IDs
    private static readonly int BlurRadiusID = Shader.PropertyToID("_BlurRadius");
    private static readonly int WaveCountRadialID = Shader.PropertyToID("_WaveCountRadial");
    private static readonly int WaveCountAngularID = Shader.PropertyToID("_WaveCountAngular");
    private static readonly int WaveAnimateID = Shader.PropertyToID("_WaveAnimate");
    private static readonly int WaveFreqRadialID = Shader.PropertyToID("_WaveFrequenciesRadial");
    private static readonly int WaveSpeedRadialID = Shader.PropertyToID("_WaveSpeedsRadial");
    private static readonly int WaveStrRadialID = Shader.PropertyToID("_WaveStrengthsRadial");
    private static readonly int WaveFreqAngularID = Shader.PropertyToID("_WaveFrequenciesAngular");
    private static readonly int WaveSpeedAngularID = Shader.PropertyToID("_WaveSpeedsAngular");
    private static readonly int WaveStrAngularID = Shader.PropertyToID("_WaveStrengthsAngular");
    private static readonly int CenterBrightnessID = Shader.PropertyToID("_CenterBrightnessMultiplier");
    private static readonly int BrightnessRangeID = Shader.PropertyToID("_BrightnessRange");
    private static readonly int BrightnessOffsetID = Shader.PropertyToID("_BrightnessOffset");
    private static readonly int BrightnessFalloffID = Shader.PropertyToID("_BrightnessFalloff");
    private static readonly int FadeStartRadiusID = Shader.PropertyToID("_FadeStartRadius");
    private static readonly int FadeEndAlphaID = Shader.PropertyToID("_FadeEndAlpha");

    private float[] currentWaveStrengthsRadial = new float[8];
    private float[] currentWaveStrengthsAngular = new float[8];

    private RadialWaveBlurPresetList presetList;

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

        if (targetMaterial == null)
        {
            var rawImg = GetComponent<RawImage>();
            if (rawImg != null)
                targetMaterial = rawImg.material;
        }
    }

    private void LoadPresets()
    {
        var asset = Resources.Load<TextAsset>("RadialWaveBlurPresets");
        if (asset != null)
            presetList = JsonUtility.FromJson<RadialWaveBlurPresetList>(asset.text);
        else
            Debug.LogWarning("[RadialWaveBlurController] RadialWaveBlurPresets.json not found in Resources folder.");
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
        waveCountRadial = p.waveCountRadial;
        waveCountAngular = p.waveCountAngular;
        waveAnimate = p.waveAnimate;

        CopyArray(p.waveFrequenciesRadial, ref waveFrequenciesRadial);
        CopyArray(p.waveSpeedsRadial, ref waveSpeedsRadial);
        CopyArray(p.waveStrengthsRadial, ref waveStrengthsRadial);
        CopyArray(p.ampVarSpeedsRadial, ref ampVarSpeedsRadial);
        CopyArray(p.waveFrequenciesAngular, ref waveFrequenciesAngular);
        CopyArray(p.waveSpeedsAngular, ref waveSpeedsAngular);
        CopyArray(p.waveStrengthsAngular, ref waveStrengthsAngular);
        CopyArray(p.ampVarSpeedsAngular, ref ampVarSpeedsAngular);

        enablePulse = p.enablePulse;
        pulseMultiplier = p.pulseMultiplier;
        centerBrightnessMultiplier = p.centerBrightnessMultiplier;
        brightnessRange = p.brightnessRange;
        brightnessOffset = p.brightnessOffset;
        brightnessFalloff = p.brightnessFalloff;
        fadeStartRadius = p.fadeStartRadius;
        fadeEndAlpha = p.fadeEndAlpha;

        if (targetMaterial != null)
        {
            System.Array.Copy(waveStrengthsRadial, currentWaveStrengthsRadial, 8);
            System.Array.Copy(waveStrengthsAngular, currentWaveStrengthsAngular, 8);
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
            System.Array.Copy(waveStrengthsRadial, currentWaveStrengthsRadial, 8);
            System.Array.Copy(waveStrengthsAngular, currentWaveStrengthsAngular, 8);
        }

        PushToMaterial();
    }

    private void OnValidate()
    {
        if (targetMaterial == null) return;

        System.Array.Copy(waveStrengthsRadial, currentWaveStrengthsRadial, 8);
        System.Array.Copy(waveStrengthsAngular, currentWaveStrengthsAngular, 8);

        PushToMaterial();

#if UNITY_EDITOR
        UnityEditor.SceneView.RepaintAll();
#endif
    }

    private void PushToMaterial()
    {
        targetMaterial.SetFloat(BlurRadiusID, blurRadius);

        targetMaterial.SetInt(WaveCountRadialID, waveCountRadial);
        targetMaterial.SetInt(WaveCountAngularID, waveCountAngular);
        targetMaterial.SetFloat(WaveAnimateID, waveAnimate ? 1f : 0f);
        targetMaterial.SetFloatArray(WaveFreqRadialID, waveFrequenciesRadial);
        targetMaterial.SetFloatArray(WaveSpeedRadialID, waveSpeedsRadial);
        targetMaterial.SetFloatArray(WaveStrRadialID, currentWaveStrengthsRadial);
        targetMaterial.SetFloatArray(WaveFreqAngularID, waveFrequenciesAngular);
        targetMaterial.SetFloatArray(WaveSpeedAngularID, waveSpeedsAngular);
        targetMaterial.SetFloatArray(WaveStrAngularID, currentWaveStrengthsAngular);

        targetMaterial.SetFloat(CenterBrightnessID, centerBrightnessMultiplier);
        targetMaterial.SetFloat(BrightnessRangeID, brightnessRange);
        targetMaterial.SetFloat(BrightnessOffsetID, brightnessOffset);
        targetMaterial.SetFloat(BrightnessFalloffID, brightnessFalloff);

        targetMaterial.SetFloat(FadeStartRadiusID, fadeStartRadius);
        targetMaterial.SetFloat(FadeEndAlphaID, fadeEndAlpha);
    }

    private void UpdateAmplitudePulse()
    {
        for (int i = 0; i < waveCountRadial; i++)
        {
            float s = Mathf.Sin(2f * Mathf.PI * Time.time * ampVarSpeedsRadial[i]);
            currentWaveStrengthsRadial[i] = Mathf.Abs(s) * pulseMultiplier * waveStrengthsRadial[i];
        }
        for (int j = 0; j < waveCountAngular; j++)
        {
            float s = Mathf.Sin(2f * Mathf.PI * Time.time * ampVarSpeedsAngular[j]);
            currentWaveStrengthsAngular[j] = Mathf.Abs(s) * pulseMultiplier * waveStrengthsAngular[j];
        }
    }
}
