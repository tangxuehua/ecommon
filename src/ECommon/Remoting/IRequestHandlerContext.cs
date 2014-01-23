using System;

namespace ECommon.Remoting
{
    public interface IRequestHandlerContext
    {
        IChannel Channel { get; }
        Action<RemotingResponse> SendRemotingResponse { get; }
    }
}
