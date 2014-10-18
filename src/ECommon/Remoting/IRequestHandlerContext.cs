using System;

namespace ECommon.Remoting
{
    public interface IRequestHandlerContext
    {
        ISocketChannel Channel { get; }
        Action<RemotingResponse> SendRemotingResponse { get; }
    }
}
