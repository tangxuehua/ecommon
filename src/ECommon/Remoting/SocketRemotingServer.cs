using System;
using System.Collections.Generic;
using System.Text;
using ECommon.Components;
using ECommon.Logging;
using ECommon.Socketing;

namespace ECommon.Remoting
{
    public class SocketRemotingServer
    {
        private readonly ServerSocket _serverSocket;
        private readonly Dictionary<int, IRequestHandler> _requestHandlerDict;
        private readonly ILogger _logger;

        public SocketRemotingServer(string name, SocketSetting socketSetting, ISocketEventListener socketEventListener = null)
        {
            _serverSocket = new ServerSocket(socketEventListener);
            _requestHandlerDict = new Dictionary<int, IRequestHandler>();
            _logger = ObjectContainer.Resolve<ILoggerFactory>().Create(name ?? GetType().Name);
            _serverSocket.Bind(socketSetting.Address, socketSetting.Port).Listen(socketSetting.Backlog);
        }

        public void Start()
        {
            _serverSocket.Start(HandleRemotingRequest);
        }

        public void Shutdown()
        {
            _serverSocket.Shutdown();
        }

        public void RegisterRequestHandler(int requestCode, IRequestHandler requestHandler)
        {
            _requestHandlerDict[requestCode] = requestHandler;
        }

        private void HandleRemotingRequest(ReceiveContext receiveContext)
        {
            var remotingRequest = RemotingUtil.ParseRequest(receiveContext.ReceivedMessage);
            IRequestHandler requestHandler;
            if (!_requestHandlerDict.TryGetValue(remotingRequest.Code, out requestHandler))
            {
                var errorMessage = string.Format("No request handler found for remoting request, request code:{0}", remotingRequest.Code);
                _logger.Error(errorMessage);
                DoMessageHandledCallback(receiveContext, remotingRequest, new RemotingResponse(-1, remotingRequest.Sequence, Encoding.UTF8.GetBytes(errorMessage)));
                return;
            }

            try
            {
                _logger.DebugFormat("Handling remoting request, request code:{0}, request sequence:{1}", remotingRequest.Code, remotingRequest.Sequence);
                var remotingResponse = requestHandler.HandleRequest(new SocketRequestHandlerContext(receiveContext), remotingRequest);
                if (!remotingRequest.IsOneway && remotingResponse != null)
                {
                    DoMessageHandledCallback(receiveContext, remotingRequest, remotingResponse);
                }
            }
            catch (Exception ex)
            {
                var errorMessage = string.Format("Exception raised when handling remoting request, request code:{0}.", remotingRequest.Code);
                _logger.Error(errorMessage, ex);
                if (!remotingRequest.IsOneway)
                {
                    DoMessageHandledCallback(receiveContext, remotingRequest, new RemotingResponse(-1, remotingRequest.Sequence, Encoding.UTF8.GetBytes(ex.Message)));
                }
            }
        }
        private void DoMessageHandledCallback(ReceiveContext receiveContext, RemotingRequest remotingRequest, RemotingResponse remotingResponse)
        {
            receiveContext.ReplyMessage = RemotingUtil.BuildResponseMessage(remotingResponse);
            receiveContext.MessageHandledCallback(receiveContext);
            _logger.DebugFormat("Remoting request handled, request code:{0}, request sequence:{1}, response code:{2}, response sequence:{3}", remotingRequest.Code, remotingRequest.Sequence, remotingResponse.Code, remotingResponse.Sequence);
        }
    }
}
