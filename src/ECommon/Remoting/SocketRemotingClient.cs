using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ECommon.Components;
using ECommon.Logging;
using ECommon.Remoting.Exceptions;
using ECommon.Scheduling;
using ECommon.Socketing;
using ECommon.Socketing.BufferManagement;
using ECommon.Utilities;

namespace ECommon.Remoting
{
    public class SocketRemotingClient
    {
        private readonly byte[] TimeoutMessage = Encoding.UTF8.GetBytes("Remoting request timeout.");
        private readonly Dictionary<int, IResponseHandler> _responseHandlerDict;
        private readonly Dictionary<int, IRemotingServerMessageHandler> _remotingServerMessageHandlerDict;
        private readonly IList<IConnectionEventListener> _connectionEventListeners;
        private readonly ConcurrentDictionary<long, ResponseFuture> _responseFutureDict;
        private readonly BlockingCollection<byte[]> _replyMessageQueue;
        private readonly IScheduleService _scheduleService;
        private readonly ILogger _logger;
        private readonly SocketSetting _setting;
        private readonly byte[] HeartbeatMessage = new byte[0];
        private int _reconnecting = 0;
        private bool _shutteddown = false;
        private bool _started = false;

        public string Name { get; }
        public bool IsConnected
        {
            get { return ClientSocket != null && ClientSocket.IsConnected; }
        }
        public EndPoint LocalEndPoint { get; private set; }
        public EndPoint ServerEndPoint { get; }
        public ClientSocket ClientSocket { get; private set; }
        public IBufferPool BufferPool { get; }

        public SocketRemotingClient(string name) : this(name, new IPEndPoint(IPAddress.Loopback, 5000)) { }
        public SocketRemotingClient(string name, EndPoint serverEndPoint, SocketSetting setting = null, EndPoint localEndPoint = null)
        {
            Ensure.NotNull(serverEndPoint, "serverEndPoint");

            Name = name;
            ServerEndPoint = serverEndPoint;
            LocalEndPoint = localEndPoint;
            _setting = setting ?? new SocketSetting();
            BufferPool = new BufferPool(_setting.ReceiveDataBufferSize, _setting.ReceiveDataBufferPoolSize);
            ClientSocket = new ClientSocket(name, ServerEndPoint, LocalEndPoint, _setting, BufferPool, HandleServerMessage);
            _responseFutureDict = new ConcurrentDictionary<long, ResponseFuture>();
            _replyMessageQueue = new BlockingCollection<byte[]>(new ConcurrentQueue<byte[]>());
            _responseHandlerDict = new Dictionary<int, IResponseHandler>();
            _remotingServerMessageHandlerDict = new Dictionary<int, IRemotingServerMessageHandler>();
            _connectionEventListeners = new List<IConnectionEventListener>();
            _scheduleService = ObjectContainer.Resolve<IScheduleService>();
            _logger = ObjectContainer.Resolve<ILoggerFactory>().Create(GetType().FullName);

            RegisterConnectionEventListener(new ConnectionEventListener(this));
        }

        public SocketRemotingClient RegisterResponseHandler(int requestCode, IResponseHandler responseHandler)
        {
            _responseHandlerDict[requestCode] = responseHandler;
            return this;
        }
        public SocketRemotingClient RegisterRemotingServerMessageHandler(int messageCode, IRemotingServerMessageHandler messageHandler)
        {
            _remotingServerMessageHandlerDict[messageCode] = messageHandler;
            return this;
        }
        public SocketRemotingClient RegisterConnectionEventListener(IConnectionEventListener listener)
        {
            _connectionEventListeners.Add(listener);
            ClientSocket.RegisterConnectionEventListener(listener);
            return this;
        }
        public SocketRemotingClient Start()
        {
            if (_started) return this;

            StartClientSocket();
            StartScanTimeoutRequestTask();
            _shutteddown = false;
            _started = true;
            return this;
        }
        public void Shutdown()
        {
            _shutteddown = true;
            StopReconnectServerTask();
            StopScanTimeoutRequestTask();
            ShutdownClientSocket();
        }

        public Task<RemotingResponse> InvokeAsync(RemotingRequest request, int timeoutMillis)
        {
            EnsureClientStatus();

            request.Type = RemotingRequestType.Async;
            var taskCompletionSource = new TaskCompletionSource<RemotingResponse>();
            var responseFuture = new ResponseFuture(request, timeoutMillis, taskCompletionSource);

            if (!_responseFutureDict.TryAdd(request.Sequence, responseFuture))
            {
                throw new ResponseFutureAddFailedException(request.Sequence);
            }

            ClientSocket.QueueMessage(RemotingUtil.BuildRequestMessage(request));

            return taskCompletionSource.Task;
        }
        public void InvokeWithCallback(RemotingRequest request)
        {
            EnsureClientStatus();

            request.Type = RemotingRequestType.Callback;
            ClientSocket.QueueMessage(RemotingUtil.BuildRequestMessage(request));
        }
        public void InvokeOneway(RemotingRequest request)
        {
            EnsureClientStatus();

            request.Type = RemotingRequestType.Oneway;
            ClientSocket.QueueMessage(RemotingUtil.BuildRequestMessage(request));
        }

        private void HandleServerMessage(ITcpConnection connection, byte[] message)
        {
            if (message == null) return;

            var remotingServerMessage = RemotingUtil.ParseRemotingServerMessage(message);

            if (remotingServerMessage.Type == RemotingServerMessageType.RemotingResponse)
            {
                HandleResponseMessage(connection, remotingServerMessage.Body);
            }
            else if (remotingServerMessage.Type == RemotingServerMessageType.ServerMessage)
            {
                HandleServerPushMessage(connection, remotingServerMessage);
            }
        }
        private void HandleResponseMessage(ITcpConnection connection, byte[] message)
        {
            if (message == null) return;

            var remotingResponse = RemotingUtil.ParseResponse(message);

            if (remotingResponse.RequestType == RemotingRequestType.Callback)
            {
                if (_responseHandlerDict.TryGetValue(remotingResponse.RequestCode, out IResponseHandler responseHandler))
                {
                    responseHandler.HandleResponse(remotingResponse);
                }
                else
                {
                    _logger.ErrorFormat("No response handler found for remoting response, name: {0}, response: {1}", Name, remotingResponse);
                }
            }
            else if (remotingResponse.RequestType == RemotingRequestType.Async)
            {
                if (_responseFutureDict.TryRemove(remotingResponse.RequestSequence, out ResponseFuture responseFuture))
                {
                    if (responseFuture.SetResponse(remotingResponse))
                    {
                        if (_logger.IsDebugEnabled)
                        {
                            _logger.DebugFormat("Remoting response back, name: {0}, request code: {1}, requect sequence: {2}, time spent: {3}", Name, responseFuture.Request.Code, responseFuture.Request.Sequence, (DateTime.Now - responseFuture.BeginTime).TotalMilliseconds);
                        }
                    }
                    else
                    {
                        _logger.ErrorFormat("Set remoting response failed, name: {0}, response: {1}", Name, remotingResponse);
                    }
                }
            }
        }
        private void HandleServerPushMessage(ITcpConnection connection, RemotingServerMessage message)
        {
            if (_remotingServerMessageHandlerDict.TryGetValue(message.Code, out IRemotingServerMessageHandler messageHandler))
            {
                messageHandler.HandleMessage(message);
            }
            else
            {
                _logger.ErrorFormat("No handler found for remoting server push message, name: {0}, message: {1}", Name, message);
            }
        }
        private void ScanTimeoutRequest()
        {
            var timeoutKeyList = new List<long>();
            foreach (var entry in _responseFutureDict)
            {
                if (entry.Value.IsTimeout())
                {
                    timeoutKeyList.Add(entry.Key);
                }
            }
            foreach (var key in timeoutKeyList)
            {
                if (_responseFutureDict.TryRemove(key, out ResponseFuture responseFuture))
                {
                    var request = responseFuture.Request;
                    responseFuture.SetResponse(new RemotingResponse(
                        request.Type,
                        request.Code,
                        request.Sequence,
                        request.CreatedTime,
                        0,
                        TimeoutMessage,
                        DateTime.Now,
                        request.Header,
                        null));
                    if (_logger.IsDebugEnabled)
                    {
                        _logger.DebugFormat("Removed timeout request, name: {0}, request: {1}", Name, responseFuture.Request);
                    }
                }
            }
        }
        private void ReconnectServer()
        {
            _logger.InfoFormat("Try to reconnect to server, name: {0}, serverAddress: {1}", Name, ServerEndPoint);

            if (ClientSocket.IsConnected) return;
            if (!EnterReconnecting()) return;

            try
            {
                ClientSocket.Shutdown();
                ClientSocket = new ClientSocket(ClientSocket.Name, ServerEndPoint, LocalEndPoint, _setting, BufferPool, HandleServerMessage);
                foreach (var listener in _connectionEventListeners)
                {
                    ClientSocket.RegisterConnectionEventListener(listener);
                }
                ClientSocket.Start();
            }
            catch (Exception ex)
            {
                _logger.Error(string.Format("Reconnect to server has exception, name: {0}, serverAddress: {1}", Name, ServerEndPoint), ex);
            }
            finally
            {
                ExitReconnecting();
            }
        }
        private void StartClientSocket()
        {
            ClientSocket.Start();
        }
        private void ShutdownClientSocket()
        {
            ClientSocket.Shutdown();
        }
        private void StartScanTimeoutRequestTask()
        {
            _scheduleService.StartTask(string.Format("{0}.{1}.ScanTimeoutRequest", Name, GetType().Name), ScanTimeoutRequest, 1000, _setting.ScanTimeoutRequestInterval);
        }
        private void StopScanTimeoutRequestTask()
        {
            _scheduleService.StopTask(string.Format("{0}.{1}.ScanTimeoutRequest", Name, GetType().Name));
        }
        private void StartReconnectServerTask()
        {
            _scheduleService.StartTask(string.Format("{0}.{1}.ReconnectServer", Name, GetType().Name), () => ReconnectServer(), 1000, _setting.ReconnectToServerInterval);
        }
        private void StopReconnectServerTask()
        {
            _scheduleService.StopTask(string.Format("{0}.{1}.ReconnectServer", Name, GetType().Name));
        }
        private void EnsureClientStatus()
        {
            if (ClientSocket == null || !ClientSocket.IsConnected)
            {
                throw new RemotingServerUnAvailableException(ServerEndPoint);
            }
        }
        private bool EnterReconnecting()
        {
            return Interlocked.CompareExchange(ref _reconnecting, 1, 0) == 0;
        }
        private void ExitReconnecting()
        {
            Interlocked.Exchange(ref _reconnecting, 0);
        }
        private void SetLocalEndPoint(EndPoint localEndPoint)
        {
            LocalEndPoint = localEndPoint;
        }

        class ConnectionEventListener : IConnectionEventListener
        {
            private readonly SocketRemotingClient _remotingClient;

            public ConnectionEventListener(SocketRemotingClient remotingClient)
            {
                _remotingClient = remotingClient;
            }

            public void OnConnectionAccepted(ITcpConnection connection) { }
            public void OnConnectionEstablished(ITcpConnection connection)
            {
                _remotingClient.StopReconnectServerTask();
                _remotingClient.ExitReconnecting();
                _remotingClient.SetLocalEndPoint(connection.LocalEndPoint);
            }
            public void OnConnectionFailed(EndPoint remotingEndPoint, SocketError socketError)
            {
                if (_remotingClient._shutteddown) return;

                _remotingClient.ExitReconnecting();
                _remotingClient.StartReconnectServerTask();
            }
            public void OnConnectionClosed(ITcpConnection connection, SocketError socketError)
            {
                if (_remotingClient._shutteddown || socketError == SocketError.Success) return;

                _remotingClient.ExitReconnecting();
                _remotingClient.StartReconnectServerTask();
            }
        }
    }
}
