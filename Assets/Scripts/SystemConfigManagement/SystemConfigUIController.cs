using System.Globalization;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class SystemConfigUIController : MonoBehaviour
{
    [Header("Bootstrapper")]
    [SerializeField]
    private SystemConfigBootstrapper configBootstrapper;

    [Header("Well Calibration")]
    [SerializeField] private InputField rotationOffsetDegreesInput;
    [SerializeField] private InputField positionOffsetXInput;
    [SerializeField] private InputField positionOffsetYInput;
    [SerializeField] private InputField scaleInput;

    [Header("WebSocket Connection")]
    [SerializeField] private InputField webSocketIpInput;
    [SerializeField] private InputField webSocketPortInput;

    [Header("Cat Appearance")]
    [SerializeField] private InputField secondsToRevealFullBodyInput;
    [SerializeField] private InputField dataInterruptToCollapseSecondsInput;
    [SerializeField] private InputField dataInterruptDestroyDelaySecondsInput;

    [Header("Screen Settings")]
    [SerializeField] private InputField widthRatioInput;
    [SerializeField] private InputField heightRatioInput;

    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

    [Header("Events")]
    [SerializeField]
    private UnityEvent onConfigApplied;

    private void Start()
    {
        LoadFromBootstrapperAndApplyToUI();
    }

    // =========================
    // Load
    // =========================

    public void LoadFromBootstrapperAndApplyToUI()
    {
        if (configBootstrapper == null)
        {
            Debug.LogError("[SystemConfigUIController] configBootstrapper is null");
            return;
        }

        SystemConfigData config = configBootstrapper.LoadConfig();
        if (config == null)
        {
            Debug.LogError("[SystemConfigUIController] LoadConfig failed. File: " + configBootstrapper.ConfigFilePath);
            return;
        }

        ApplyConfigToUI(config);
    }

    private void ApplyConfigToUI(SystemConfigData config)
    {
        if (config == null)
            return;

        if (config.wellCalibration != null)
        {
            Set(rotationOffsetDegreesInput, config.wellCalibration.rotationOffsetDegrees);

            if (config.wellCalibration.positionOffset != null)
            {
                Set(positionOffsetXInput, config.wellCalibration.positionOffset.x);
                Set(positionOffsetYInput, config.wellCalibration.positionOffset.y);
            }

            Set(scaleInput, config.wellCalibration.scale);
        }

        if (config.webSocketConnection != null)
        {
            Set(webSocketIpInput, config.webSocketConnection.ip);
            Set(webSocketPortInput, config.webSocketConnection.port);
        }

        if (config.catAppearance != null)
        {
            Set(secondsToRevealFullBodyInput, config.catAppearance.secondsToRevealFullBody);
            Set(dataInterruptToCollapseSecondsInput, config.catAppearance.SecondsPersonLeavesTemporarily);
            Set(dataInterruptDestroyDelaySecondsInput, config.catAppearance.SecondsPersonLeavesPermanently);
        }

        if (config.screenSettings != null)
        {
            Set(widthRatioInput, config.screenSettings.widthRatio);
            Set(heightRatioInput, config.screenSettings.heightRatio);
        }
        onConfigApplied?.Invoke();
    }

    // =========================
    // Save
    // =========================

    public void SaveFromUIToBootstrapper()
    {
        if (configBootstrapper == null)
        {
            Debug.LogError("[SystemConfigUIController] configBootstrapper is null");
            return;
        }

        SystemConfigData config = BuildConfigFromUI();
        configBootstrapper.SaveConfig(config);

        Debug.Log("[SystemConfigUIController] Config saved. File: " + configBootstrapper.ConfigFilePath);
    }

    private SystemConfigData BuildConfigFromUI()
    {
        var config = new SystemConfigData();

        config.wellCalibration = new WellCalibrationConfig();
        config.wellCalibration.rotationOffsetDegrees = GetFloat(rotationOffsetDegreesInput);

        config.wellCalibration.positionOffset = new PositionOffset();
        config.wellCalibration.positionOffset.x = GetFloat(positionOffsetXInput);
        config.wellCalibration.positionOffset.y = GetFloat(positionOffsetYInput);

        config.wellCalibration.scale = GetFloat(scaleInput);

        config.webSocketConnection = new WebSocketConnectionConfig();
        config.webSocketConnection.ip = GetString(webSocketIpInput);
        config.webSocketConnection.port = GetInt(webSocketPortInput);

        config.catAppearance = new CatAppearanceConfig();
        config.catAppearance.secondsToRevealFullBody = GetFloat(secondsToRevealFullBodyInput);
        config.catAppearance.SecondsPersonLeavesTemporarily = GetFloat(dataInterruptToCollapseSecondsInput);
        config.catAppearance.SecondsPersonLeavesPermanently = GetFloat(dataInterruptDestroyDelaySecondsInput);

        config.screenSettings = new ScreenSettingsConfig();
        config.screenSettings.widthRatio = GetInt(widthRatioInput);
        config.screenSettings.heightRatio = GetInt(heightRatioInput);

        return config;
    }

    // =========================
    // Helpers
    // =========================

    private void Set(InputField input, float value)
    {
        if (input != null)
            input.text = value.ToString(Invariant);
    }

    private void Set(InputField input, int value)
    {
        if (input != null)
            input.text = value.ToString(Invariant);
    }

    private void Set(InputField input, string value)
    {
        if (input != null)
            input.text = value ?? "";
    }

    private float GetFloat(InputField input)
    {
        if (input == null) return 0f;

        string s = input.text ?? "";
        if (float.TryParse(s, NumberStyles.Float, Invariant, out float v))
            return v;

        return 0f;
    }

    private int GetInt(InputField input)
    {
        if (input == null) return 0;

        string s = input.text ?? "";
        if (int.TryParse(s, NumberStyles.Integer, Invariant, out int v))
            return v;

        return 0;
    }

    private string GetString(InputField input)
    {
        return input != null ? (input.text ?? "") : "";
    }
}
