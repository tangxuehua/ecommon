using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using ECommon.Components;
using ECommon.Logging;
using ECommon.Scheduling;

namespace ECommon.Socketing
{
    public class ServerSocket
    {
        private Socket _socket;
        private Action<ReceiveContext> _messageReceivedCallback;
        private ManualResetEvent _newClientSocketSignal;
        private SocketService _socketService;
        private ISocketEventListener _socketEventListener;
        private Worker _listenNewClientWorker;
        private ILogger _logger;
        private bool _isRunning;

        public ServerSocket(ISocketEventListener socketEventListener)
        {
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _socketEventListener = socketEventListener;
            _socketService = new SocketService(socketEventListener);
            _newClientSocketSignal = new ManualResetEvent(false);
            _logger = ObjectContainer.Resolve<ILoggerFactory>().Create(GetType().FullName);
        }

        public ServerSocket Listen(int backlog)
        {
            _socket.Listen(backlog);
            return this;
        }
        public ServerSocket Bind(string address, int port)
        {
            _socket.Bind(new IPEndPoint(IPAddress.Parse(address), port));
            return this;
        }
        public void Start(Action<ReceiveContext> messageReceivedCallback)
        {
            if (_isRunning) return;

            _isRunning = true;

            _listenNewClientWorker = new Worker("ServerSocket.AcceptNewClient", () =>
            {
                if (!_isRunning) return;

                _messageReceivedCallback = messageReceivedCallback;
                _newClientSocketSignal.Reset();

                try
                {
                    _socket.BeginAccept((asyncResult) =>
                    {
                        if (!_isRunning) return;

                        var clientSocket = _socket.EndAccept(asyncResult);
                        var socketInfo = new SocketInfo(clientSocket);
                        NotifyNewSocketAccepted(socketInfo);
                        _newClientSocketSignal.Set();

                        if (!_isRunning) return;

                        _socketService.ReceiveMessage(socketInfo, receivedMessage =>
                        {
                            var receiveContext = new ReceiveContext(socketInfo, receivedMessage, context =>
                            {
                                _socketService.SendMessage(context.ReplySocketInfo, context.ReplyMessage, sendResult =>
                                {
                                    if (context.ReplySentCallback != null)
                                    {
                                        context.ReplySentCallback(sendResult);
                                    }
                                });
                            });
                            _messageReceivedCallback(receiveContext);
                        });
                    }, _socket);
                }
                catch (SocketException socketException)
                {
                    _logger.Error(string.Format("Socket accept exception, ErrorCode:{0}", socketException.SocketErrorCode), socketException);
                }
                catch (Exception ex)
                {
                    _logger.Error("Unknown socket accept exception.", ex);
                }

                if (_isRunning)
                {
                    _newClientSocketSignal.WaitOne();
                }
            });
            _listenNewClientWorker.Start();
        }
        public void Shutdown()
        {
            _isRunning = false;
            try
            {
                _socket.Shutdown(SocketShutdown.Both);
            }
            catch { }
            try
            {
                _socket.Close();
            }
            catch
            { }

            _listenNewClientWorker.Stop();
            if (_newClientSocketSignal != null)
            {
                _newClientSocketSignal.Set();
            }
        }

        private void NotifyNewSocketAccepted(SocketInfo socketInfo)
        {
            if (_socketEventListener != null)
            {
                Task.Factory.StartNew(() => _socketEventListener.OnNewSocketAccepted(socketInfo));
            }
        }
    }
}
