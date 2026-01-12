using Newtonsoft.Json;
using System.IO;
using UnityEngine;

public class ExperienceCounterBootstrapper : MonoBehaviour
{
    [Header("Experience Counter Location")]

    [Tooltip("JSON 所在的資料夾名稱（位於 dataPath 底下）")]
    [SerializeField]
    private string counterFolderName = "ExperienceCounter";

    [Tooltip("JSON 檔名（不含 .json）")]
    [SerializeField]
    private string counterFileName = "experience_counter";

    public string CounterFolderPath
    {
        get
        {
            return Path.Combine(Application.dataPath, counterFolderName);
        }
    }

    public string CounterFilePath
    {
        get
        {
            return Path.Combine(
                CounterFolderPath,
                $"{counterFileName}.json"
            );
        }
    }

    private void Awake()
    {
        EnsureCounterExists();
    }

    private void EnsureCounterExists()
    {
        string folderPath = CounterFolderPath;
        string fullPath = CounterFilePath;

        // 確保資料夾存在
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
            Debug.Log("[ExperienceCounterBootstrapper] Created folder: " + folderPath);
        }

        // 若檔案存在，嘗試載入
        if (File.Exists(fullPath))
        {
            LoadAndLogCounter(fullPath);
            return;
        }

        // 不存在 → 建立 default
        CreateDefaultCounter(fullPath);
    }

    private void LoadAndLogCounter(string fullPath)
    {
        Debug.Log("[ExperienceCounterBootstrapper] Counter found. Loading: " + fullPath);

        string json = File.ReadAllText(fullPath);

        ExperienceCounterData data =
            JsonConvert.DeserializeObject<ExperienceCounterData>(json);

        if (data == null)
        {
            Debug.LogError("[ExperienceCounterBootstrapper] Failed to deserialize counter.");
            return;
        }
    }

    private void CreateDefaultCounter(string fullPath)
    {
        Debug.Log("[ExperienceCounterBootstrapper] Counter not found. Creating default counter: " + fullPath);

        ExperienceCounterData defaultData =
            ExperienceCounterDefaults.Create();

        string json = JsonConvert.SerializeObject(
            defaultData,
            Formatting.Indented
        );

        File.WriteAllText(fullPath, json);
    }

    // ===== 對外 API（與 SystemConfig 一致） =====

    public ExperienceCounterData LoadCounter()
    {
        if (!File.Exists(CounterFilePath))
            return null;

        string json = File.ReadAllText(CounterFilePath);
        return JsonConvert.DeserializeObject<ExperienceCounterData>(json);
    }

    public void SaveCounter(ExperienceCounterData data)
    {
        string json = JsonConvert.SerializeObject(
            data,
            Formatting.Indented
        );

        File.WriteAllText(CounterFilePath, json);
    }
}
