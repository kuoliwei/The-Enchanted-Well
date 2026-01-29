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

    bool disconnectionMessageLock = false;

    private void Start()
    {
        //ipInput.text = "127.0.0.1";
        //portInput.text = "9999";

        //ipInput.text = "127.0.0.1";
        //portInput.text = "8765";

        connectButton.onClick.AddListener(OnClickConnect);

        receiver.OnConnected += NotifyConnectionSucceeded;
        receiver.OnConnectionFailed += NotifyConnectionFailed;
        receiver.OnDisconnected += NotifyDisconnected;
    }
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            connectPanel.SetActive(!connectPanel.activeSelf);
        }
    }
    public void ConnectFromExternal()
    {
        OnClickConnect();
    }

    private void OnClickConnect()
    {
        disconnectionMessageLock = true;
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
        // 不論狀態，直接中斷
        receiver.Close();
        // 呼叫 Receiver 做連線（UI 不處理連線細節）
        receiver.Connect(ip, portString);
    }

    /// <summary>
    /// 供 Receiver 在成功時呼叫
    /// </summary>
    public void NotifyConnectionSucceeded()
    {
        if (connectPanel.activeSelf)
        {
            SetResult(true, "連線成功");
            Debug.Log("[WebSocketConnectUI] 連線成功");
        }
        else
            Debug.Log("[WebSocketConnectUI] 連線成功");
        disconnectionMessageLock = false;
        //connectPanel.SetActive(false);
    }

    /// <summary>
    /// 供 Receiver 在連線失敗時呼叫
    /// </summary>
    public void NotifyConnectionFailed(string reason = "連線失敗")
    {
        if (connectPanel.activeSelf)
        {
            SetResult(false, reason);
            Debug.Log("[WebSocketConnectUI] 連線失敗");
        }
        else
            Debug.Log("[WebSocketConnectUI] 連線失敗");
    }
    /// <summary>
    /// 供 Receiver 在斷線時呼叫
    /// </summary>
    public void NotifyDisconnected()
    {
        if (!disconnectionMessageLock)
        {
            // 若 UI 面板有開，顯示給使用者
            if (connectPanel.activeSelf)
            {
                SetResult(false, "連線中斷");
                Debug.Log("[WebSocketConnectUI] 連線中斷");
            }
            else
            {
                Debug.Log("[WebSocketConnectUI] 連線中斷");
            }
        }
        else
        {
            disconnectionMessageLock = false;
        }
    }


    private void SetResult(bool isSuccess, string msg)
    {
        message.text = msg;
        OnConnectResult?.Invoke(isSuccess, msg);
    }
}
