using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WebSokcetServerSendTest : MonoBehaviour
{
    [TextArea(3,10)]
    [SerializeField] private string sendMessage;
    [SerializeField] private WebSocketServer.WebSocketServer webSocketServer;

    [ContextMenu("SendMessageTest")]
    private void SendMessageToClient()
    {
        webSocketServer.CallSendMessage(sendMessage);
    }
}
