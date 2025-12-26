using System;
using System.Collections.Concurrent;
using UnityEngine;
using UnityEngine.Events;

public class WebSocketMessageReceiverAsync : MonoBehaviour
{
    [Header("WebSocket 客戶端")]
    [SerializeField] private WebSocketClient webSocketClient;

    [Header("是否允許接收訊息")]
    public bool CanReceiveMessages = true;

    public event Action<string> OnRawJsonReceived;

    // ★ 新增：供 UI 或外部訂閱（成功 / 失敗 / 斷線）
    public event Action OnConnected;
    public event Action<string> OnConnectionFailed;
    public event Action OnDisconnected;

    // 多執行緒訊息佇列（WebSocket thread → Unity main thread）
    private readonly ConcurrentQueue<string> rawJsonQueue = new();

    private void Start()
    {
        if (webSocketClient != null)
        {
            // WebSocket 收到訊息（在背景 thread）
            webSocketClient.OnMessageReceive.AddListener(HandleIncomingMessage);

            // ★ 新增連線成功事件
            webSocketClient.OnConnected.AddListener(() =>
            {
                Debug.Log("[WS] Connected");
                OnConnected?.Invoke();
            });

            // ★ 新增連線失敗事件
            webSocketClient.OnConnectionError.AddListener(() =>
            {
                Debug.LogError("[WS] Connection Error");
                OnConnectionFailed?.Invoke("連線失敗");
            });

            // ★ 新增斷線事件
            webSocketClient.OnDisconnected.AddListener(() =>
            {
                Debug.LogWarning("[WS] Disconnected");
                OnDisconnected?.Invoke();
            });
        }
    }

    private void Update()
    {
        // 將 background queue 的訊息送回 Unity main thread
        while (rawJsonQueue.TryDequeue(out var json))
        {
            OnRawJsonReceived?.Invoke(json);
        }
    }

    /// <summary>
    /// 背景 thread 收到 JSON 時呼叫
    /// </summary>
    private void HandleIncomingMessage(string json)
    {
        //Debug.Log($"[Client Raw Receive] 收到資料:\n{json}");
        if (!CanReceiveMessages)
            return;

        rawJsonQueue.Enqueue(json);
    }

    // ------------------------------
    // 公用方法：Connect / Send / Close
    // ------------------------------

    public void Connect(string ip, string port)
    {
        string address = $"ws://{ip}:{port}";
        Debug.Log($"[WS] Connecting to {address}");

        webSocketClient?.CloseConnection();
        webSocketClient?.StartConnection(address);
    }

    public void Send(string message)
    {
        if (webSocketClient == null)
        {
            Debug.LogWarning("[WebSocketMessageReceiverAsync] webSocketClient 未設定");
            return;
        }

        webSocketClient.SendSocketMessage(message);
    }

    public void Close()
    {
        webSocketClient?.CloseConnection();
    }
}
