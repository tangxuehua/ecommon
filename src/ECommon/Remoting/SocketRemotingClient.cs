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
using ECommon.TcpTransport;
using TcpSocketClient = ECommon.TcpTransport.TcpClient;

namespace ECommon.Remoting
{
    public class SocketRemotingClient : ISocketClientEventListener
    {
        private readonly byte[] TimeoutMessage = Encoding.UTF8.GetBytes("Remoting request timeout.");
        private readonly RemotingClientSetting _setting;
        private readonly TcpSocketClient _tcpClient;
        private readonly object _sync;
        private readonly IPEndPoint _serverEndPoint;
        private readonly ConcurrentDictionary<long, ResponseFuture> _responseFutureDict;
        private readonly BlockingCollection<byte[]> _replyMessageQueue;
        private readonly IScheduleService _scheduleService;
        private readonly ILogger _logger;
        private readonly ISocketClientEventListener _eventListener;
        private readonly Worker _worker;
        private int _scanTimeoutRequestTaskId;
        private int _isReconnecting;

        public SocketRemotingClient(IPEndPoint serverEndPoint, RemotingClientSetting setting = null, ISocketClientEventListener eventListener = null)
        {
            _sync = new object();
            _serverEndPoint = serverEndPoint;
            _eventListener = eventListener;
            _setting = setting ?? new RemotingClientSetting();
            _tcpClient = new TcpSocketClient(_setting.LocalEndPoint, serverEndPoint, ReceiveReplyMessage, this);
            _responseFutureDict = new ConcurrentDictionary<long, ResponseFuture>();
            _replyMessageQueue = new BlockingCollection<byte[]>(new ConcurrentQueue<byte[]>());
            _scheduleService = ObjectContainer.Resolve<IScheduleService>();
            _worker = new Worker("SocketRemotingClient.HandleReplyMessage", HandleReplyMessage);
            _logger = ObjectContainer.Resolve<ILoggerFactory>().Create(GetType().FullName);
        }

        public void Start(int connectTimeoutMilliseconds = 5000)
        {
            StartTcpClient(connectTimeoutMilliseconds);
            StartHandleMessageWorker();
            StartScanTimeoutRequestTask();
        }
        public void Shutdown()
        {
            StopScanTimeoutRequestTask();
            StopHandleMessageWorker();
            StopTcpClient();
        }
        public RemotingResponse InvokeSync(RemotingRequest request, int timeoutMillis = 5000)
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
        public Task<RemotingResponse> InvokeAsync(RemotingRequest request, int timeoutMillis = 5000)
        {
            EnsureClientStatus();

            var taskCompletionSource = new TaskCompletionSource<RemotingResponse>();
            var responseFuture = new ResponseFuture(request, timeoutMillis, taskCompletionSource);

            if (!_responseFutureDict.TryAdd(request.Sequence, responseFuture))
            {
                throw new ResponseFutureAddFailedException(request.Sequence);
            }

            _tcpClient.SendAsync(RemotingUtil.BuildRequestMessage(request));

            return taskCompletionSource.Task;
        }
        public void InvokeOneway(RemotingRequest request)
        {
            EnsureClientStatus();

            request.IsOneway = true;
            _tcpClient.SendAsync(RemotingUtil.BuildRequestMessage(request));
        }

        private void ReceiveReplyMessage(byte[] message)
        {
            _replyMessageQueue.Add(message);
        }
        private void HandleReplyMessage()
        {
            var responseMessage = _replyMessageQueue.Take();

            if (responseMessage == null) return;

            var remotingResponse = RemotingUtil.ParseResponse(responseMessage);

            ResponseFuture responseFuture;
            if (_responseFutureDict.TryRemove(remotingResponse.Sequence, out responseFuture))
            {
                responseFuture.SetResponse(remotingResponse);
                _logger.DebugFormat("Remoting response back, request code:{0}, requect sequence:{1}, time spent:{2}", responseFuture.Request.Code, responseFuture.Request.Sequence, (DateTime.Now - responseFuture.BeginTime).TotalMilliseconds);
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
                    responseFuture.SetResponse(new RemotingResponse(0, responseFuture.Request.Sequence, TimeoutMessage));
                    _logger.DebugFormat("Removed timeout request:{0}", responseFuture.Request);
                }
            }
        }
        private void ReconnectServer()
        {
            if (_tcpClient.IsStopped)
            {
                _logger.Error("Not allowed to reconnect server as the tcp client is stopped.");
                return;
            }
            if (_tcpClient.ConnectionStatus == TcpConnectionStatus.Established)
            {
                return;
            }
            if (Interlocked.CompareExchange(ref _isReconnecting, 1, 0) != 0)
            {
                _logger.Info("Reconnect server is in progress, ignore the current reconnecting.");
                return;
            }

            Thread.Sleep(_setting.ReconnectInterval);

            _logger.InfoFormat("Try to reconnect to server:{0}.", _serverEndPoint);

            var hasException = false;
            try
            {
                _tcpClient.ReconnectToServer();
            }
            catch (Exception ex)
            {
                _logger.ErrorFormat("Reconnect to server has exception.", ex);
                hasException = true;
            }
            finally
            {
                Interlocked.Exchange(ref _isReconnecting, 0);
            }
            if (hasException)
            {
                ReconnectServer();
            }
        }
        private void StartTcpClient(int connectTimeoutMilliseconds = 5000)
        {
            _tcpClient.Start(connectTimeoutMilliseconds);
        }
        private void StopTcpClient()
        {
            _tcpClient.Stop();
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
            lock (_sync)
            {
                if (_scanTimeoutRequestTaskId == 0)
                {
                    _scanTimeoutRequestTaskId = _scheduleService.ScheduleTask("SocketRemotingClient.ScanTimeoutRequest", ScanTimeoutRequest, 1000, 1000);
                }
            }
        }
        private void StopScanTimeoutRequestTask()
        {
            lock (_sync)
            {
                if (_scanTimeoutRequestTaskId > 0)
                {
                    _scheduleService.ShutdownTask(_scanTimeoutRequestTaskId);
                    _scanTimeoutRequestTaskId = 0;
                }
            }
        }
        private void EnsureClientStatus()
        {
            if (_tcpClient.ConnectionStatus != TcpConnectionStatus.Established)
            {
                throw new RemotingServerNotConnectedException(_serverEndPoint, _tcpClient.ConnectionStatus);
            }
        }

        void ISocketClientEventListener.OnConnectionEstablished(ITcpConnectionInfo connectionInfo)
        {
            if (_eventListener != null)
            {
                _eventListener.OnConnectionEstablished(connectionInfo);
            }
        }
        void ISocketClientEventListener.OnConnectionFailed(ITcpConnectionInfo connectionInfo, SocketError socketError)
        {
            if (_eventListener != null)
            {
                _eventListener.OnConnectionFailed(connectionInfo, socketError);
            }
            ReconnectServer();
        }
        void ISocketClientEventListener.OnConnectionClosed(ITcpConnectionInfo connectionInfo, SocketError socketError)
        {
            if (_eventListener != null)
            {
                _eventListener.OnConnectionClosed(connectionInfo, socketError);
            }
            ReconnectServer();
        }
    }
}
