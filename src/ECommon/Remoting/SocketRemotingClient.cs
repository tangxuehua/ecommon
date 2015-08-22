using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ECommon.Components;
using ECommon.Extensions;
using ECommon.Logging;
using ECommon.Remoting.Exceptions;
using ECommon.Scheduling;
using ECommon.Socketing;

namespace ECommon.Remoting
{
    public class SocketRemotingClient
    {
        private readonly byte[] TimeoutMessage = Encoding.UTF8.GetBytes("Remoting request timeout.");
        private EndPoint _serverEndPoint;
        private EndPoint _localEndPoint;
        private ClientSocket _clientSocket;
        private readonly object _lockObject1;
        private readonly object _lockObject2;
        private readonly Dictionary<int, IResponseHandler> _responseHandlerDict;
        private readonly IList<IConnectionEventListener> _connectionEventListeners;
        private readonly ConcurrentDictionary<long, ResponseFuture> _responseFutureDict;
        private readonly BlockingCollection<byte[]> _replyMessageQueue;
        private readonly IScheduleService _scheduleService;
        private readonly ILogger _logger;
        private readonly Worker _worker;
        private int _scanTimeoutRequestTaskId;
        private int _reconnectServerTaskId;
        private int _reconnecting = 0;

        public SocketRemotingClient() : this(new IPEndPoint(SocketUtils.GetLocalIPV4(), 5000)) { }
        public SocketRemotingClient(EndPoint serverEndPoint, EndPoint localEndPoint = null)
        {
            _lockObject1 = new object();
            _lockObject2 = new object();
            _serverEndPoint = serverEndPoint;
            _localEndPoint = localEndPoint;
            _clientSocket = new ClientSocket(serverEndPoint, localEndPoint, ReceiveReplyMessage);
            _responseFutureDict = new ConcurrentDictionary<long, ResponseFuture>();
            _replyMessageQueue = new BlockingCollection<byte[]>(new ConcurrentQueue<byte[]>());
            _responseHandlerDict = new Dictionary<int, IResponseHandler>();
            _connectionEventListeners = new List<IConnectionEventListener>();
            _scheduleService = ObjectContainer.Resolve<IScheduleService>();
            _worker = new Worker("SocketRemotingClient.HandleReplyMessage", HandleReplyMessage);
            _logger = ObjectContainer.Resolve<ILoggerFactory>().Create(GetType().FullName);

            RegisterConnectionEventListener(new ConnectionEventListener(this));
        }

        public SocketRemotingClient RegisterResponseHandler(int requestCode, IResponseHandler responseHandler)
        {
            _responseHandlerDict[requestCode] = responseHandler;
            return this;
        }
        public SocketRemotingClient RegisterConnectionEventListener(IConnectionEventListener listener)
        {
            _connectionEventListeners.Add(listener);
            _clientSocket.RegisterConnectionEventListener(listener);
            return this;
        }
        public SocketRemotingClient Start()
        {
            StartClientSocket();
            StartHandleMessageWorker();
            StartScanTimeoutRequestTask();
            return this;
        }
        public void Shutdown()
        {
            StopReconnectServerTask();
            StopScanTimeoutRequestTask();
            StopHandleMessageWorker();
            ShutdownClientSocket();
        }

        public RemotingResponse InvokeSync(RemotingRequest request, int timeoutMillis)
        {
            var task = InvokeAsync(request, timeoutMillis);
            var response = task.WaitResult<RemotingResponse>(timeoutMillis + 1000);

            if (response == null)
            {
                if (!task.IsCompleted)
                {
                    throw new RemotingTimeoutException(_serverEndPoint, request, timeoutMillis);
                }
                else if (task.IsFaulted)
                {
                    throw new RemotingRequestException(_serverEndPoint, request, task.Exception);
                }
                else
                {
                    throw new RemotingRequestException(_serverEndPoint, request, "Remoting response is null due to unkown exception.");
                }
            }
            return response;
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

            _clientSocket.SendAsync(RemotingUtil.BuildRequestMessage(request));

            return taskCompletionSource.Task;
        }
        public void InvokeWithCallback(RemotingRequest request)
        {
            EnsureClientStatus();

            request.Type = RemotingRequestType.Callback;
            _clientSocket.SendAsync(RemotingUtil.BuildRequestMessage(request));
        }
        public void InvokeOneway(RemotingRequest request)
        {
            EnsureClientStatus();

            request.Type = RemotingRequestType.Oneway;
            _clientSocket.SendAsync(RemotingUtil.BuildRequestMessage(request));
        }

        private void ReceiveReplyMessage(ITcpConnection connection, byte[] message)
        {
            _replyMessageQueue.Add(message);
        }
        private void HandleReplyMessage()
        {
            var responseMessage = _replyMessageQueue.Take();

            if (responseMessage == null) return;

            var remotingResponse = RemotingUtil.ParseResponse(responseMessage);

            if (remotingResponse.Type == RemotingRequestType.Callback)
            {
                IResponseHandler responseHandler;
                if (_responseHandlerDict.TryGetValue(remotingResponse.RequestCode, out responseHandler))
                {
                    responseHandler.HandleResponse(remotingResponse);
                }
            }
            else if (remotingResponse.Type == RemotingRequestType.Async)
            {
                ResponseFuture responseFuture;
                if (_responseFutureDict.TryRemove(remotingResponse.Sequence, out responseFuture))
                {
                    if (responseFuture.SetResponse(remotingResponse))
                    {
                        _logger.DebugFormat("Remoting response back, request code:{0}, requect sequence:{1}, time spent:{2}", responseFuture.Request.Code, responseFuture.Request.Sequence, (DateTime.Now - responseFuture.BeginTime).TotalMilliseconds);
                    }
                    else
                    {
                        _logger.ErrorFormat("Set remoting response failed, response:" + remotingResponse);
                    }
                }
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
                ResponseFuture responseFuture;
                if (_responseFutureDict.TryRemove(key, out responseFuture))
                {
                    responseFuture.SetResponse(new RemotingResponse(responseFuture.Request.Code, 0, responseFuture.Request.Type, TimeoutMessage, responseFuture.Request.Sequence));
                    _logger.DebugFormat("Removed timeout request:{0}", responseFuture.Request);
                }
            }
        }
        private void ReconnectServer()
        {
            _logger.InfoFormat("Try to reconnect to server, remote endpoint:{0}", _serverEndPoint);

            if (_clientSocket.IsConnected) return;
            if (!EnterReconnecting()) return;

            try
            {
                _clientSocket.Shutdown();
                _clientSocket = new ClientSocket(_serverEndPoint, _localEndPoint, ReceiveReplyMessage);
                foreach (var listener in _connectionEventListeners)
                {
                    _clientSocket.RegisterConnectionEventListener(listener);
                }
                _clientSocket.Start();
            }
            catch (Exception ex)
            {
                _logger.Error("Reconnect to server error.", ex);
                ExitReconnecting();
            }
        }
        private void StartClientSocket()
        {
            _clientSocket.Start();
        }
        private void ShutdownClientSocket()
        {
            _clientSocket.Shutdown();
        }
        private void StartHandleMessageWorker()
        {
            _worker.Start();
        }
        private void StopHandleMessageWorker()
        {
            _worker.Stop();
            if (_replyMessageQueue.Count == 0)
            {
                _replyMessageQueue.Add(null);
            }
        }
        private void StartScanTimeoutRequestTask()
        {
            lock (_lockObject1)
            {
                if (_scanTimeoutRequestTaskId == 0)
                {
                    _scanTimeoutRequestTaskId = _scheduleService.ScheduleTask("SocketRemotingClient.ScanTimeoutRequest", ScanTimeoutRequest, 1000, 1000);
                }
            }
        }
        private void StopScanTimeoutRequestTask()
        {
            lock (_lockObject1)
            {
                if (_scanTimeoutRequestTaskId > 0)
                {
                    _scheduleService.ShutdownTask(_scanTimeoutRequestTaskId);
                    _scanTimeoutRequestTaskId = 0;
                }
            }
        }
        private void StartReconnectServerTask()
        {
            lock (_lockObject2)
            {
                if (_reconnectServerTaskId == 0)
                {
                    _reconnectServerTaskId = _scheduleService.ScheduleTask("SocketRemotingClient.ReconnectServer", ReconnectServer, 1000, 1000);
                }
            }
        }
        private void StopReconnectServerTask()
        {
            lock (_lockObject2)
            {
                if (_reconnectServerTaskId > 0)
                {
                    _scheduleService.ShutdownTask(_reconnectServerTaskId);
                    _reconnectServerTaskId = 0;
                }
            }
        }
        private void EnsureClientStatus()
        {
            if (!_clientSocket.IsConnected)
            {
                throw new RemotingServerUnAvailableException(_serverEndPoint);
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
            }
            public void OnConnectionFailed(SocketError socketError)
            {
                _remotingClient.ExitReconnecting();
                _remotingClient.StartReconnectServerTask();
            }
            public void OnConnectionClosed(ITcpConnection connection, SocketError socketError)
            {
                _remotingClient.ExitReconnecting();
                _remotingClient.StartReconnectServerTask();
            }
        }
    }
}
