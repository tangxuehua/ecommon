using System;
using System.Net.Sockets;
using ECommon.Logging;
using ECommon.Socketing;

namespace ECommon.Remoting
{
    public class SocketRequestHandlerContext : IRequestHandlerContext
    {
        public IChannel Channel { get; private set; }
        public Action<RemotingResponse> SendRemotingResponse { get; private set; }

        public SocketRequestHandlerContext(ReceiveContext receiveContext, RemotingRequest remotingRequest, ILogger logger)
        {
            Channel = new SocketChannel(receiveContext.ReplySocketInfo);
            SendRemotingResponse = remotingResponse =>
            {
                receiveContext.ReplyMessage = RemotingUtil.BuildResponseMessage(remotingResponse);
                receiveContext.ReplySentCallback = sendResult =>
                {
                    if (!sendResult.Success && sendResult.Exception != null)
                    {
                        var errorMessage = "[" + sendResult.Exception.GetType().Name + "]";
                        var socketException = sendResult.Exception as SocketException;
                        if (socketException != null)
                        {
                            errorMessage = "[" + sendResult.Exception.GetType().Name + ", ErrorCode:" + socketException.SocketErrorCode + "]";
                        }
                        logger.DebugFormat("Remoting request handled, reply sent status:{0}, errorMessage:{1}, request code:{2}, request sequence:{3}, response code:{4}, response sequence:{5}", sendResult.Success, errorMessage, remotingRequest.Code, remotingRequest.Sequence, remotingResponse.Code, remotingResponse.Sequence);
                    }
                    else
                    {
                        logger.DebugFormat("Remoting request handled, reply sent status:{0}, request code:{1}, request sequence:{2}, response code:{3}, response sequence:{4}", sendResult.Success, remotingRequest.Code, remotingRequest.Sequence, remotingResponse.Code, remotingResponse.Sequence);
                    }
                };
                logger.DebugFormat("Begin to send reply, request code:{0}, request sequence:{1}, response code:{2}, response sequence:{3}", remotingRequest.Code, remotingRequest.Sequence, remotingResponse.Code, remotingResponse.Sequence);
                receiveContext.MessageHandledCallback(receiveContext);
            };
        }
    }
}
