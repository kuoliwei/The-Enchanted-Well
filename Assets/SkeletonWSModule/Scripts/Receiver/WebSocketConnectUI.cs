using UnityEngine;
using UnityEngine.UI;
using System;

public class WebSocketConnectUI : MonoBehaviour
{
    [Header("UI 元件")]
    [SerializeField] private Text message;
    [SerializeField] private GameObject connectPanel;
    [SerializeField] private InputField ipInput;
    [SerializeField] private InputField portInput;
    [SerializeField] private Button connectButton;

    [Header("WebSocket 接收器（負責真正連線）")]
    public WebSocketMessageReceiverAsync receiver;

    /// <summary>
    /// 連線結果事件（true=成功, false=失敗, string=訊息）
    /// </summary>
    public event Action<bool, string> OnConnectResult;

    private void Start()
    {
        //ipInput.text = "127.0.0.1";
        //portInput.text = "9999";

        //ipInput.text = "127.0.0.1";
        //portInput.text = "8765";

        connectButton.onClick.AddListener(OnClickConnect);

        receiver.OnConnected += NotifyConnectionSucceeded;
        receiver.OnConnectionFailed += NotifyConnectionFailed;
    }

    private void OnClickConnect()
    {
        message.text = "";

        string ip = ipInput.text.Trim();
        string portString = portInput.text.Trim();

        if (!System.Net.IPAddress.TryParse(ip, out _))
        {
            SetResult(false, "IP 格式不正確");
            return;
        }

        if (!int.TryParse(portString, out int port) || port < 1 || port > 65535)
        {
            SetResult(false, "Port 格式不正確（有效範圍 1~65535）");
            return;
        }

        // 呼叫 Receiver 做連線（UI 不處理連線細節）
        receiver.Connect(ip, portString);
    }

    /// <summary>
    /// 供 Receiver 在成功時呼叫
    /// </summary>
    public void NotifyConnectionSucceeded()
    {
        SetResult(true, "連線成功");
        connectPanel.SetActive(false);
    }

    /// <summary>
    /// 供 Receiver 在連線失敗時呼叫
    /// </summary>
    public void NotifyConnectionFailed(string reason = "連線失敗")
    {
        if (connectPanel.activeSelf)
            SetResult(false, reason);
        else
            Debug.LogWarning(reason);
    }

    private void SetResult(bool isSuccess, string msg)
    {
        message.text = msg;
        OnConnectResult?.Invoke(isSuccess, msg);
    }
}
