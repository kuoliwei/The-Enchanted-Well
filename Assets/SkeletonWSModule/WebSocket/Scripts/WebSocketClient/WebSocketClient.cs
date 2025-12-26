using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityWebSocket;
using UnityEngine.Events;
using System.Net;

public class WebSocketClient : MonoBehaviour
{
    public string ipAddress = "127.0.0.1";
    [SerializeField] private bool connectOnStart = true;
    public bool IsConnected {get{ return socket != null && socket.ReadyState == WebSocketState.Open; }}
    [HideInInspector] public bool allowReconnect = false;
    [HideInInspector] public bool isReconnectAttempt = false;
    WebSocket socket = null;
    public WebSocketState CurrentState => socket.ReadyState;
    [SerializeField] float timeoutTick = 5f;
    float timeoutTimer = 0f;

    public UnityEvent<string> OnMessageReceive = new();

    [Header("Debug")]
    [SerializeField] private bool showDebug = false;
    public UnityEvent OnConnected = new();
    public UnityEvent OnConnectionError = new();
    public UnityEvent OnDisconnected = new();


    private void Start()
    {
        if (connectOnStart) StartConnection();
    }

    private void Update()
    {
        if (allowReconnect)
        {
            TryReconnect();
        }
    }

    private void OnDestroy()
    {
        CloseConnection();
    }

    public void StartConnection(string newIP = "")
    {
        if (newIP != string.Empty)
        {
            ipAddress = newIP;

        }

        socket = new WebSocket($"{ipAddress}");
        socket.OnOpen += OnOpen;
        socket.OnClose += OnClose;
        socket.OnMessage += OnMessage;
        socket.OnError += OnError;
        socket.ConnectAsync();
    }

    public void CloseConnection()
    {
        if (socket == null) return;
        socket.CloseAsync();
        socket.OnOpen -= OnOpen;
        socket.OnClose -= OnClose;
        socket.OnMessage -= OnMessage;
        socket.OnError -= OnError;
        socket = null;
    }

    private void OnOpen(object sender, OpenEventArgs args)
    {
        if (showDebug) Debug.LogWarning($"OnOpen");
        OnConnected?.Invoke(); // 通知 UI 或其他模組
    }

    private void OnClose(object sender, CloseEventArgs args)
    {
        if (showDebug) Debug.LogWarning($"OnClose");
        OnDisconnected?.Invoke();
        CloseConnection();
    }

    private void OnMessage(object sender, MessageEventArgs args)
    {
        if (showDebug) Debug.LogWarning($"OnMessage:{args.Data}");
        OnMessageReceive?.Invoke(args.Data);
    }

    private void OnError(object sender, ErrorEventArgs args)
    {
        if (showDebug) Debug.LogWarning($"OnError: {args.Message}");
        OnConnectionError?.Invoke();
    }

    private void TryReconnect()
    {
        if (IsConnected || (socket != null && socket.ReadyState == WebSocketState.Connecting)) return;
        if (timeoutTimer >= timeoutTick)
        {
            if (showDebug) Debug.LogWarning("Try reconnect to websocket server.");
            StartConnection();
            timeoutTimer = 0;
        }
        else
        {
            timeoutTimer += Time.deltaTime;
        }
    }

    public void SendSocketMessage(string message)
    {
        if (socket.ReadyState != WebSocketState.Open) return;
        socket.SendAsync(message);
        Debug.Log($"{System.DateTime.Now} WebSocket send {message}");
    }
}
