using UnityEngine;
using System;

public class WebSocketServerManager : MonoBehaviour
{
    public WebSocketServer.WebSocketServer server;

    /// <summary>
    /// 當收到文字訊息時，廣播 raw JSON 字串
    /// </summary>
    public event Action<string> OnRawMessageReceived;

    private void Awake()
    {
        if (server == null)
        {
            Debug.LogError("[WebSocketServerManager] server 未設定！");
            return;
        }

        // 訂閱 WebSocket 原始訊息事件
        server.onMessage.AddListener(HandleRawMessage);
    }

    private void HandleRawMessage(WebSocketServer.WebSocketMessage msg)
    {
        string json = msg.data;
        OnRawMessageReceived?.Invoke(json);
    }

    public void Send(string rawJson)
    {
        server.SendMessageToClient(rawJson);
    }

    private void OnDestroy()
    {
        if (server != null)
            server.onMessage.RemoveListener(HandleRawMessage);
    }
}
