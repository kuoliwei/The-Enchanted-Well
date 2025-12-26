using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;

public class RollerSimulator : MonoBehaviour
{
    [Header("WebSocket 發送器")]
    public WebSocketServerManager ws;

    [Header("Local JSON 資料夾名稱（相對於 Application.dataPath）")]
    public string folderName = "PoseJson";

    [Header("播放速度 (每秒幾筆資料)")]
    public float sendRate = 15f;

    [Header("是否循環播放")]
    public bool loop = true;

    private float timer = 0f;

    /// <summary>
    /// 依照檔名數字排序後的完整路徑清單
    /// </summary>
    private List<string> jsonPaths = new List<string>();

    private int currentIndex = 0;

    private bool allowSending = false;

    private void Start()
    {
        if (ws == null)
        {
            Debug.LogError("[RollerSimulator] ws 未設定！");
            enabled = false;
            return;
        }

        LoadJsonFiles();
    }

    /// <summary>
    /// 從指定資料夾載入所有 JSON 檔案
    /// </summary>
    private void LoadJsonFiles()
    {
        string fullPath = Path.Combine(Application.dataPath, folderName);

        if (!Directory.Exists(fullPath))
        {
            Debug.LogError($"[RollerSimulator] 找不到資料夾：{fullPath}");
            return;
        }

        var files = Directory.GetFiles(fullPath, "*.json", SearchOption.TopDirectoryOnly);

        if (files.Length == 0)
        {
            Debug.LogError($"[RollerSimulator] 資料夾中沒有 JSON 檔案：{fullPath}");
            return;
        }

        jsonPaths = files
            .OrderBy(path => ExtractNumber(Path.GetFileNameWithoutExtension(path)))
            .ToList();

        Debug.Log($"[RollerSimulator] 已載入 {jsonPaths.Count} 個 JSON 檔案");
    }

    /// <summary>
    /// 從檔名擷取數字（若沒有數字 → 回傳 int.MaxValue）
    /// </summary>
    private int ExtractNumber(string fileName)
    {
        string digits = new string(fileName.Where(char.IsDigit).ToArray());

        if (int.TryParse(digits, out int num))
            return num;

        return int.MaxValue;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.P))
        {
            allowSending = !allowSending;
        }

        if (allowSending)
        {
            if (jsonPaths.Count == 0)
                return;

            timer += Time.deltaTime;

            if (timer >= 1f / sendRate)
            {
                timer = 0f;
                SendNextJson();
            }
        }
    }

    /// <summary>
    /// 手動重置播放進度
    /// </summary>
    public void ResetPlayback()
    {
        currentIndex = 0;
        Debug.Log("[RollerSimulator] Playback has been reset to index 0.");
    }

    private void SendNextJson()
    {
        if (currentIndex >= jsonPaths.Count)
        {
            if (loop)
            {
                currentIndex = 0; // 循環播放
            }
            else
            {
                allowSending = false; // 停止播放
                Debug.Log("[RollerSimulator] 播放已結束（未勾選 loop）");
                return;
            }
        }

        string path = jsonPaths[currentIndex];
        string json = File.ReadAllText(path);

        // 發送原始 JSON 給 WebSocket
        ws.Send(json);

        currentIndex++;
    }
}
