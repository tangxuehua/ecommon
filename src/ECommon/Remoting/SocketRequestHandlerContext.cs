using System;
using ECommon.TcpTransport;

namespace ECommon.Remoting
{
    public class SocketRequestHandlerContext : IRequestHandlerContext
    {
        public ISocketChannel Channel { get; private set; }
        public Action<RemotingResponse> SendRemotingResponse { get; private set; }

        public SocketRequestHandlerContext(ITcpConnection connection, Action<byte[]> sendReplyAction)
        {
            Channel = new SocketChannel(connection);
            SendRemotingResponse = remotingResponse =>
            {
                sendReplyAction(RemotingUtil.BuildResponseMessage(remotingResponse));
            };
        }
    }
}
