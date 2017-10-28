using System;
using ECommon.Socketing;

namespace ECommon.Remoting
{
    public class SocketRequestHandlerContext : IRequestHandlerContext
    {
        public ITcpConnection Connection { get; private set; }
        public Action<RemotingResponse> SendRemotingResponse { get; private set; }

        public SocketRequestHandlerContext(ITcpConnection connection, Action<byte[]> sendReplyAction)
        {
            Connection = connection;
            SendRemotingResponse = remotingResponse =>
            {
                sendReplyAction(BuildRemotingServerMessage(remotingResponse));
            };
        }

        private static byte[] BuildRemotingServerMessage(RemotingResponse remotingResponse)
        {
            byte[] remotingResponseData = RemotingUtil.BuildResponseMessage(remotingResponse);
            var remotingServerMessage = new RemotingServerMessage(
                RemotingServerMessageType.RemotingResponse,
                100,
                remotingResponseData,
                null);
            return RemotingUtil.BuildRemotingServerMessage(remotingServerMessage);
        }
    }
}
