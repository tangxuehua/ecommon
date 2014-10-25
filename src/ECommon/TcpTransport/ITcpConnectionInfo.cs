using System;
using System.Net;

namespace ECommon.TcpTransport
{
    public interface ITcpConnectionInfo
    {
        Guid ConnectionId { get; }
        IPEndPoint RemoteEndPoint { get; }
        IPEndPoint LocalEndPoint { get; }
    }
}