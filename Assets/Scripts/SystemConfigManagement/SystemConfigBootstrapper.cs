using Newtonsoft.Json;
using System.IO;
using UnityEngine;

public class SystemConfigBootstrapper : MonoBehaviour
{
    [Header("System Config Location")]

    [Tooltip("JSON 所在的資料夾名稱（位於 dataPath 底下）")]
    [SerializeField]
    private string configFolderName = "SystemConfig";

    [Tooltip("JSON 檔名（不含 .json）")]
    [SerializeField]
    private string configFileName = "system_config";

    public string ConfigFolderPath
    {
        get
        {
            return Path.Combine(Application.dataPath, configFolderName);
        }
    }

    public string ConfigFilePath
    {
        get
        {
            return Path.Combine(
                ConfigFolderPath,
                $"{configFileName}.json"
            );
        }
    }

    private void Awake()
    {
        EnsureConfigExists();
    }

    private void EnsureConfigExists()
    {
        // 組合完整資料夾路徑
        string folderPath = Path.Combine(
            Application.dataPath,
            configFolderName
        );

        // 組合完整檔案路徑（含 .json）
        string fullPath = Path.Combine(
            folderPath,
            $"{configFileName}.json"
        );

        // 確保資料夾存在
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
            Debug.Log("[SystemConfigBootstrapper] Created folder: " + folderPath);
        }

        // 如果檔案存在，讀取並解析
        if (File.Exists(fullPath))
        {
            LoadAndLogConfig(fullPath);
            return;
        }

        // 如果檔案不存在，建立 default 設定檔
        CreateDefaultConfig(fullPath);
    }

    private void LoadAndLogConfig(string fullPath)
    {
        Debug.Log("[SystemConfigBootstrapper] Config found. Loading: " + fullPath);

        string json = File.ReadAllText(fullPath);

        SystemConfigData config =
            JsonConvert.DeserializeObject<SystemConfigData>(json);

        if (config == null)
        {
            Debug.LogError("[SystemConfigBootstrapper] Failed to deserialize config.");
            return;
        }
    }


    private void CreateDefaultConfig(string fullPath)
    {
        Debug.Log("[SystemConfigBootstrapper] Config not found. Creating default config: " + fullPath);

        SystemConfigData defaultConfig =
            SystemConfigDefaults.Create();

        string json = JsonConvert.SerializeObject(
            defaultConfig,
            Formatting.Indented
        );

        File.WriteAllText(fullPath, json);
    }
    public SystemConfigData LoadConfig()
    {
        if (!File.Exists(ConfigFilePath))
            return null;

        string json = File.ReadAllText(ConfigFilePath);
        return JsonConvert.DeserializeObject<SystemConfigData>(json);
    }

    public void SaveConfig(SystemConfigData config)
    {
        string json = JsonConvert.SerializeObject(
            config,
            Formatting.Indented
        );
        File.WriteAllText(ConfigFilePath, json);
    }

}
