using System;
using System.Net.Sockets;
using ECommon.Components;
using ECommon.Logging;
using ECommon.Scheduling;

namespace ECommon.Socketing
{
    public class ClientSocket
    {
        private string _address;
        private int _port;
        private Socket _socket;
        private SocketInfo _socketInfo;
        private SocketService _socketService;
        private Worker _receiveMessageWorker;
        private ILogger _logger;

        public ClientSocket(ISocketEventListener socketEventListener)
        {
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _socketService = new SocketService(socketEventListener);
            _logger = ObjectContainer.Resolve<ILoggerFactory>().Create(GetType().FullName);
        }

        public ClientSocket Connect(string address, int port)
        {
            _address = address;
            _port = port;
            ConnectInternal();
            return this;
        }
        public bool Reconnect()
        {
            try
            {
                ConnectInternal();
                return true;
            }
            catch
            {
                return false;
            }
        }
        public ClientSocket Start(Action<byte[]> replyMessageReceivedCallback)
        {
            _receiveMessageWorker = new Worker(() =>
            {
                _socketService.ReceiveMessage(new SocketInfo(_socket), replyMessage =>
                {
                    try
                    {
                        replyMessageReceivedCallback(replyMessage);
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex);
                    }
                });
            });
            _receiveMessageWorker.Start();
            return this;
        }
        public ClientSocket Shutdown()
        {
            _receiveMessageWorker.Stop();
            _socket.Shutdown(SocketShutdown.Send);
            _socket.Close();
            return this;
        }
        public ClientSocket SendMessage(byte[] messageContent, Action<SendResult> callback)
        {
            _socketService.SendMessage(_socketInfo, messageContent, callback);
            return this;
        }

        private void ConnectInternal()
        {
            _socket.Connect(_address, _port);
            _socketInfo = new SocketInfo(_socket);
        }
    }
}
