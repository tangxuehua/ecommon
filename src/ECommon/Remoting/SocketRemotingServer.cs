using System;
using System.Collections.Generic;
using System.Text;
using ECommon.IoC;
using ECommon.Logging;
using ECommon.Socketing;

namespace ECommon.Remoting
{
    public class SocketRemotingServer
    {
        private readonly ServerSocket _serverSocket;
        private readonly Dictionary<int, IRequestHandler> _requestHandlerDict;
        private readonly ILogger _logger;
        private bool _started;

        public SocketRemotingServer(string name, SocketSetting socketSetting, ISocketEventListener socketEventListener = null)
        {
            _serverSocket = new ServerSocket(socketEventListener);
            _requestHandlerDict = new Dictionary<int, IRequestHandler>();
            _logger = ObjectContainer.Resolve<ILoggerFactory>().Create(name ?? GetType().Name);
            _serverSocket.Bind(socketSetting.Address, socketSetting.Port).Listen(socketSetting.Backlog);
            _started = false;
        }

        public void Start()
        {
            if (_started) return;

            _serverSocket.Start(HandleRemotingRequest);

            _started = true;
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
                var remotingResponse = new RemotingResponse(-1, remotingRequest.Sequence, Encoding.UTF8.GetBytes(errorMessage));
                receiveContext.ReplyMessage = RemotingUtil.BuildResponseMessage(remotingResponse);
                receiveContext.MessageHandledCallback(receiveContext);
                return;
            }

            try
            {
                var remotingResponse = requestHandler.HandleRequest(new SocketRequestHandlerContext(receiveContext), remotingRequest);
                if (!remotingRequest.IsOneway && remotingResponse != null)
                {
                    receiveContext.ReplyMessage = RemotingUtil.BuildResponseMessage(remotingResponse);
                    receiveContext.MessageHandledCallback(receiveContext);
                }
            }
            catch (Exception ex)
            {
                var errorMessage = string.Format("Exception raised when handling remoting request, request code:{0}.", remotingRequest.Code);
                _logger.Error(errorMessage, ex);
                if (!remotingRequest.IsOneway)
                {
                    var remotingResponse = new RemotingResponse(-1, remotingRequest.Sequence, Encoding.UTF8.GetBytes(ex.Message));
                    receiveContext.ReplyMessage = RemotingUtil.BuildResponseMessage(remotingResponse);
                    receiveContext.MessageHandledCallback(receiveContext);
                }
            }
        }
    }
}
