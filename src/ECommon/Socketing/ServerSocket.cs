using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using ECommon.Extensions;
using ECommon.IoC;
using ECommon.Logging;
using ECommon.Scheduling;

namespace ECommon.Socketing
{
    public class ServerSocket
    {
        private Socket _socket;
        private ConcurrentDictionary<string, SocketInfo> _clientSocketDict;
        private Action<ReceiveContext> _messageReceivedCallback;
        private ManualResetEvent _newClientSocketSignal;
        private SocketService _socketService;
        private ISocketEventListener _socketEventListener;
        private IScheduleService _scheduleService;
        private ILogger _logger;
        private bool _running;

        public ServerSocket(ISocketEventListener socketEventListener)
        {
            _socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _clientSocketDict = new ConcurrentDictionary<string, SocketInfo>();
            _socketEventListener = socketEventListener;
            _socketService = new SocketService(NotifySocketReceiveException);
            _newClientSocketSignal = new ManualResetEvent(false);
            _scheduleService = ObjectContainer.Resolve<IScheduleService>();
            _logger = ObjectContainer.Resolve<ILoggerFactory>().Create(GetType().Name);
            _running = false;
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
            Task.Factory.StartNew(() =>
            {
                _messageReceivedCallback = messageReceivedCallback;
                _running = true;

                while (_running)
                {
                    _newClientSocketSignal.Reset();

                    try
                    {
                        _socket.BeginAccept((asyncResult) =>
                        {
                            var clientSocket = _socket.EndAccept(asyncResult);
                            var socketInfo = new SocketInfo(clientSocket);
                            _clientSocketDict.TryAdd(socketInfo.SocketRemotingEndpointAddress, socketInfo);
                            NotifyNewSocketAccepted(socketInfo);
                            _newClientSocketSignal.Set();
                            _socketService.ReceiveMessage(socketInfo, receivedMessage =>
                            {
                                var receiveContext = new ReceiveContext(socketInfo, receivedMessage, context =>
                                {
                                    _socketService.SendMessage(context.ReplySocketInfo.InnerSocket, context.ReplyMessage, sendResult => { });
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

                    _newClientSocketSignal.WaitOne();
                }
            });
        }
        public void Shutdown()
        {
            _socket.Shutdown(SocketShutdown.Both);
            _socket.Close();
            _running = false;
        }

        private void NotifyNewSocketAccepted(SocketInfo socketInfo)
        {
            if (_socketEventListener != null)
            {
                Task.Factory.StartNew(() => _socketEventListener.OnNewSocketAccepted(socketInfo));
            }
        }
        private void NotifySocketReceiveException(SocketInfo socketInfo, Exception exception)
        {
            if (_socketEventListener != null)
            {
                Task.Factory.StartNew(() =>
                {
                    _clientSocketDict.Remove(socketInfo.SocketRemotingEndpointAddress);
                    _socketEventListener.OnSocketReceiveException(socketInfo, exception);
                });
            }
        }
    }
}
