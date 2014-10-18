using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using ECommon.Components;
using ECommon.Logging;
using ECommon.Socketing;
using ECommon.TcpTransport;

namespace ECommon.Remoting
{
    public class SocketRemotingServer
    {
        private readonly TcpServerListener _serverListener;
        private readonly Dictionary<int, IRequestHandler> _requestHandlerDict;
        private readonly ILogger _logger;

        public SocketRemotingServer(string name, IPEndPoint serverEndPoint, ISocketServerEventListener eventListener = null)
        {
            _serverListener = new TcpServerListener(serverEndPoint, eventListener, HandleRemotingRequest);
            _requestHandlerDict = new Dictionary<int, IRequestHandler>();
            _logger = ObjectContainer.Resolve<ILoggerFactory>().Create(name ?? GetType().FullName);
        }

        public void Start()
        {
            _serverListener.Start();
        }
        public void Shutdown()
        {
            _serverListener.Stop();
        }

        public void RegisterRequestHandler(int requestCode, IRequestHandler requestHandler)
        {
            _requestHandlerDict[requestCode] = requestHandler;
        }

        private void HandleRemotingRequest(ITcpConnection connection, byte[] message, Action<byte[]> sendReplyAction)
        {
            var remotingRequest = RemotingUtil.ParseRequest(message);
            var requestHandlerContext = new SocketRequestHandlerContext(connection, sendReplyAction);

            IRequestHandler requestHandler;
            if (!_requestHandlerDict.TryGetValue(remotingRequest.Code, out requestHandler))
            {
                var errorMessage = string.Format("No request handler found for remoting request, request code:{0}", remotingRequest.Code);
                _logger.Error(errorMessage);
                requestHandlerContext.SendRemotingResponse(new RemotingResponse(-1, remotingRequest.Sequence, Encoding.UTF8.GetBytes(errorMessage)));
                return;
            }

            try
            {
                _logger.DebugFormat("Handling remoting request, request code:{0}, request sequence:{1}", remotingRequest.Code, remotingRequest.Sequence);
                var remotingResponse = requestHandler.HandleRequest(requestHandlerContext, remotingRequest);
                if (!remotingRequest.IsOneway && remotingResponse != null)
                {
                    requestHandlerContext.SendRemotingResponse(remotingResponse);
                }
            }
            catch (Exception ex)
            {
                var errorMessage = string.Format("Exception raised when handling remoting request, request code:{0}.", remotingRequest.Code);
                _logger.Error(errorMessage, ex);
                if (!remotingRequest.IsOneway)
                {
                    requestHandlerContext.SendRemotingResponse(new RemotingResponse(-1, remotingRequest.Sequence, Encoding.UTF8.GetBytes(ex.Message)));
                }
            }
        }
    }
}
