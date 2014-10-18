using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace ECommon.TcpTransport
{
    public interface ITcpConnectionInfo
    {
        Guid ConnectionId { get; }
        IPEndPoint RemoteEndPoint { get; }
        IPEndPoint LocalEndPoint { get; }
    }
}