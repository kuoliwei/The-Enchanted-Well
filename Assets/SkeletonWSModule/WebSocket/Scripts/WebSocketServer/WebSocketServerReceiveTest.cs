using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using WebSocketServer;

public class WebSocketServerReceiveTest : MonoBehaviour
{
    [SerializeField] private WebSocketServer.WebSocketServer server;
    public UnityEvent<string> OnMessageReceive = new();

    private void DecodeMessage(WebSocketMessage webSocketMessage)
    {
        OnMessageReceive?.Invoke(webSocketMessage.data);
    }

    private void Awake()
    {
        server.onMessage.AddListener(DecodeMessage);
    }
}
