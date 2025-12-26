using System;
// Networking libs
using System.Net;
using System.Net.Sockets;
// For creating a thread
using System.Threading;
// For List & ConcurrentQueue
using System.Collections.Generic;
using System.Collections.Concurrent;
// Encoding
using System.Text;
// Unity & Unity events
using UnityEngine;
using UnityEngine.Events;

namespace WebSocketServer {
    [System.Serializable]
    public class WebSocketOpenEvent : UnityEvent<WebSocketConnection> {}

    [System.Serializable]
    public class WebSocketMessageEvent : UnityEvent<WebSocketMessage> {}

    [System.Serializable]
    public class WebSocketCloseEvent : UnityEvent<WebSocketConnection> {}

    public class WebSocketServer : MonoBehaviour
    {
        // The tcpListenerThread listens for incoming WebSocket connections, then assigns the client to handler threads;
        private TcpListener tcpListener;
        private Thread tcpListenerThread;
        private List<Thread> workerThreads;
        private List<TcpClient> connectedTcpClients = new List<TcpClient>();

        public ConcurrentQueue<WebSocketEvent> events;

        public string address;
        public int port;
        public WebSocketOpenEvent onOpen;
        public WebSocketMessageEvent onMessage;
        public WebSocketCloseEvent onClose;
        public UnityEvent<int> OnConnectionCountChange;

        void Awake() {
            if (onMessage == null) onMessage = new WebSocketMessageEvent();
        }

        void Start() {
            events = new ConcurrentQueue<WebSocketEvent>();
            workerThreads = new List<Thread>();

            tcpListenerThread = new Thread (new ThreadStart(ListenForTcpConnection));
            tcpListenerThread.IsBackground = true;
            tcpListenerThread.Start();
        }

        void Update() {
            WebSocketEvent wsEvent;
            while (events.TryDequeue(out wsEvent)) {
                if (wsEvent.type == WebSocketEventType.Open) {
                    onOpen.Invoke(wsEvent.connection);
                    this.OnOpen(wsEvent.connection);
                } else if (wsEvent.type == WebSocketEventType.Close) {
                    onClose.Invoke(wsEvent.connection);
                    this.OnClose(wsEvent.connection);
                } else if (wsEvent.type == WebSocketEventType.Message) {
                    WebSocketMessage message = new WebSocketMessage(wsEvent.connection, wsEvent.data);
                    onMessage.Invoke(message);
                    this.OnMessage(message);
                }
            }
        }

        private void ListenForTcpConnection () { 		
            try {
                // Create listener on <address>:<port>.
                tcpListener = new TcpListener(IPAddress.Parse(address), port);
                tcpListener.Start();
                Debug.Log("WebSocket server is listening for incoming connections.");
                while (true) {
                    // Accept a new client, then open a stream for reading and writing.
                    var client = tcpListener.AcceptTcpClient();
                    connectedTcpClients.Add(client);
                    // Create a new connection
                    WebSocketConnection connection = new WebSocketConnection(client, this);
                    // Establish connection
                    connection.Establish();
                    // // Start a new thread to handle the connection.
                    // Thread worker = new Thread (new ParameterizedThreadStart(HandleConnection));
                    // worker.IsBackground = true;
                    // worker.Start(connection);
                    // // Add it to the thread list. TODO: delete thread when disconnecting.
                    // workerThreads.Add(worker);
                }
            }
            catch (SocketException socketException) {
                Debug.Log("SocketException " + socketException.ToString());
            }
        }

        // private void HandleConnection (object parameter) {
        //     WebSocketConnection connection = (WebSocketConnection)parameter;
        //     while (true) {
        //         string message = ReceiveMessage(connection.client, connection.stream);
        //         connection.queue.Enqueue(message);
        //     }
        // }

        // private string ReceiveMessage(TcpClient client, NetworkStream stream) {
        //     // Wait for data to be available, then read the data.
        //     while (!stream.DataAvailable);
        //     Byte[] bytes = new Byte[client.Available];
        //     stream.Read(bytes, 0, bytes.Length);

        //     return WebSocketProtocol.DecodeMessage(bytes);
        // }

        public virtual void OnOpen(WebSocketConnection connection) {}

        public virtual void OnMessage(WebSocketMessage message) { }

        public virtual void OnClose(WebSocketConnection connection) {}

        public virtual void OnError(WebSocketConnection connection) {}

        public void CallSendMessage(string msg)
        {
            SendMessageToClient(msg);
        }

        public virtual void SendMessageToClient(string msg)
        {
            for (int i = 0; i < connectedTcpClients.Count; i++)
            {
                var client = connectedTcpClients[i];
                if (!client.Connected)
                {
                    connectedTcpClients.Remove(client);
                }
                else
                {
                    NetworkStream stream = client.GetStream();
                    Queue<string> que = new Queue<string>(msg.SplitInGroups(125));
                    int len = que.Count;

                    while (que.Count > 0)
                    {
                        var header = GetHeader(
                            que.Count > 1 ? false : true,
                            que.Count == len ? false : true
                        );

                        byte[] list = Encoding.UTF8.GetBytes(que.Dequeue());
                        header = (header << 7) + list.Length;
                        stream.Write(IntToByteArray((ushort)header), 0, 2);
                        stream.Write(list, 0, list.Length);
                    }
                }
            }
        }

        protected int GetHeader(bool finalFrame, bool contFrame)
        {
            int header = finalFrame ? 1 : 0;//fin: 0 = more frames, 1 = final frame
            header = (header << 1) + 0;//rsv1
            header = (header << 1) + 0;//rsv2
            header = (header << 1) + 0;//rsv3
            header = (header << 4) + (contFrame ? 0 : 1);//opcode : 0 = continuation frame, 1 = text
            header = (header << 1) + 0;//mask: server -> client = no mask

            return header;
        }


        protected byte[] IntToByteArray(ushort value)
        {
            var ary = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(ary);
            }

            return ary;
        }
        
        /// use this method because smaller messsages
        private byte[] encodeMessage(string msg)
        {
            int msgLength = msg.Length;
            byte[] msgEncoded = new byte[msgLength + 5];
            int indexAt = 0;

            msgEncoded[0] = 0x81;
            indexAt++;

            if (msgLength <= 125)
            {
                msgEncoded[1] = (byte)msgLength;
                indexAt++;
            } else if (msgLength >= 126 && msgLength <= 65535)
            {
                msgEncoded[1] = 126;
                msgEncoded[2] = (byte)((byte)msgLength >> 8);
                msgEncoded[3] = (byte)msgLength;
                indexAt += 3;
            }

            for (int i = 0; i < msgLength; i++)
            {
                msgEncoded[indexAt + i] = (byte)msg[i];
            }
            return msgEncoded;
        }

        private void OnDestroy()
        {
            foreach(var client in connectedTcpClients)
            {
                try
                {
                    client.Close();
                }
                catch(ObjectDisposedException)
                {
                    continue;
                }
            }
            tcpListener.Stop();
        }
    }
}

public static class XLExtensions
{
    public static IEnumerable<string> SplitInGroups(this string original, int size)
    {
        var p = 0;
        var l = original.Length;
        while (l - p > size)
        {
            yield return original.Substring(p, size);
            p += size;
        }
        yield return original.Substring(p);
    }
}