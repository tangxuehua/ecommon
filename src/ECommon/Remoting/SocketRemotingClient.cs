using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net.Sockets;
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
        private readonly string _address;
        private readonly int _port;
        private readonly ClientSocket _clientSocket;
        private readonly ConcurrentDictionary<long, ResponseFuture> _responseFutureDict;
        private readonly BlockingCollection<byte[]> _responseMessageQueue;
        private readonly IScheduleService _scheduleService;
        private readonly ILogger _logger;
        private readonly ISocketEventListener _socketEventListener;
        private readonly Worker _processResponseMessageWorker;
        private readonly Worker _reconnectWorker;
        private int _scanTimeoutRequestTaskId;

        public SocketRemotingClient() : this(SocketUtils.GetLocalIPV4().ToString(), 5000) { }
        public SocketRemotingClient(string address, int port, ISocketEventListener socketEventListener = null)
        {
            _address = address;
            _port = port;
            _socketEventListener = socketEventListener;
            _clientSocket = new ClientSocket(new RemotingClientSocketEventListener(this));
            _responseFutureDict = new ConcurrentDictionary<long, ResponseFuture>();
            _responseMessageQueue = new BlockingCollection<byte[]>(new ConcurrentQueue<byte[]>());
            _scheduleService = ObjectContainer.Resolve<IScheduleService>();
            _processResponseMessageWorker = new Worker(ProcessResponseMessage);
            _reconnectWorker = new Worker(ReconnectServer, 1000);
            _logger = ObjectContainer.Resolve<ILoggerFactory>().Create(GetType().FullName);
        }

        public void Connect()
        {
            _clientSocket.Connect(_address, _port);
        }
        public void Start()
        {
            _clientSocket.Start(responseMessage => _responseMessageQueue.Add(responseMessage));
            _processResponseMessageWorker.Start();
            _scanTimeoutRequestTaskId = _scheduleService.ScheduleTask(ScanTimeoutRequest, 1000 * 3, 1000);
        }
        public void Shutdown()
        {
            _reconnectWorker.Stop();
            _processResponseMessageWorker.Stop();
            _scheduleService.ShutdownTask(_scanTimeoutRequestTaskId);
            _clientSocket.Shutdown();
        }
        public RemotingResponse InvokeSync(RemotingRequest request, int timeoutMillis)
        {
            var message = RemotingUtil.BuildRequestMessage(request);
            var taskCompletionSource = new TaskCompletionSource<RemotingResponse>();
            var responseFuture = new ResponseFuture(request, timeoutMillis, taskCompletionSource);

            if (!_responseFutureDict.TryAdd(request.Sequence, responseFuture))
            {
                throw new Exception(string.Format("Try to add response future failed. request sequence:{0}", request.Sequence));
            }

            _clientSocket.SendMessage(message, sendResult => SendMessageCallback(responseFuture, request, _address, sendResult));

            var response = taskCompletionSource.Task.WaitResult<RemotingResponse>(timeoutMillis);
            if (response == null)
            {
                if (responseFuture.SendRequestSuccess)
                {
                    throw new RemotingTimeoutException(_address, request, timeoutMillis);
                }
                else
                {
                    throw new RemotingSendRequestException(_address, request, responseFuture.SendException);
                }
            }
            return response;
        }
        public Task<RemotingResponse> InvokeAsync(RemotingRequest request, int timeoutMillis)
        {
            var message = RemotingUtil.BuildRequestMessage(request);
            var taskCompletionSource = new TaskCompletionSource<RemotingResponse>();
            var responseFuture = new ResponseFuture(request, timeoutMillis, taskCompletionSource);

            if (!_responseFutureDict.TryAdd(request.Sequence, responseFuture))
            {
                throw new Exception(string.Format("Try to add response future failed. request sequence:{0}", request.Sequence));
            }

            _clientSocket.SendMessage(message, sendResult => SendMessageCallback(responseFuture, request, _address, sendResult));

            return taskCompletionSource.Task;
        }
        public void InvokeOneway(RemotingRequest request, int timeoutMillis)
        {
            request.IsOneway = true;
            _clientSocket.SendMessage(RemotingUtil.BuildRequestMessage(request), x => { });
        }

        private void ProcessResponseMessage()
        {
            var responseMessage = _responseMessageQueue.Take();
            var remotingResponse = RemotingUtil.ParseResponse(responseMessage);

            ResponseFuture responseFuture;
            if (_responseFutureDict.TryRemove(remotingResponse.Sequence, out responseFuture))
            {
                responseFuture.CompleteRequestTask(remotingResponse);
            }
        }
        private void ScanTimeoutRequest()
        {
            var timeoutResponseFutureKeyList = new List<long>();
            foreach (var entry in _responseFutureDict)
            {
                if (entry.Value.IsTimeout())
                {
                    timeoutResponseFutureKeyList.Add(entry.Key);
                }
            }
            foreach (var key in timeoutResponseFutureKeyList)
            {
                ResponseFuture responseFuture;
                if (_responseFutureDict.TryRemove(key, out responseFuture))
                {
                    responseFuture.CompleteRequestTask(null);
                    _logger.InfoFormat("Removed timeout request:{0}", responseFuture.Request);
                }
            }
        }
        private void SendMessageCallback(ResponseFuture responseFuture, RemotingRequest request, string address, SendResult sendResult)
        {
            responseFuture.SendRequestSuccess = sendResult.Success;
            responseFuture.SendException = sendResult.Exception;
            if (!sendResult.Success)
            {
                _logger.ErrorFormat("Send request {0} to channel <{1}> failed, exception:{2}", request, address, sendResult.Exception);
                responseFuture.CompleteRequestTask(null);
                _responseFutureDict.Remove(request.Sequence);
            }
        }
        private void ReconnectServer()
        {
            if (_clientSocket.Reconnect())
            {
                _reconnectWorker.Stop();
            }
        }

        class RemotingClientSocketEventListener : ISocketEventListener
        {
            private SocketRemotingClient _socketRemotingClient;

            public RemotingClientSocketEventListener(SocketRemotingClient socketRemotingClient)
            {
                _socketRemotingClient = socketRemotingClient;
            }

            public void OnNewSocketAccepted(SocketInfo socketInfo)
            {
                if (_socketRemotingClient._socketEventListener != null)
                {
                    _socketRemotingClient._socketEventListener.OnNewSocketAccepted(socketInfo);
                }
            }

            public void OnSocketException(SocketInfo socketInfo, SocketException socketException)
            {
                if (SocketUtils.IsSocketDisconnectedException(socketException))
                {
                    _socketRemotingClient._logger.DebugFormat("Server[address={0}] disconnected, start to reconnect.", socketInfo.SocketRemotingEndpointAddress);
                    _socketRemotingClient._reconnectWorker.Start();
                }
                else
                {
                    _socketRemotingClient._logger.Error(string.Format("SocketException[address={0},errorCode={1}]", socketInfo.SocketRemotingEndpointAddress, socketException.SocketErrorCode), socketException);
                }

                if (_socketRemotingClient._socketEventListener != null)
                {
                    _socketRemotingClient._socketEventListener.OnSocketException(socketInfo, socketException);
                }
            }
        }
    }
}
