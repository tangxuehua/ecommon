using System;
using System.Collections.Generic;
using System.Net.Sockets;

namespace ECommon.TcpTransport
{
    public interface ITcpConnection : ITcpConnectionInfo
    {
        event Action<ITcpConnection, SocketError> ConnectionClosed;
        bool IsClosed { get; }

        void ReceiveAsync(Action<ITcpConnection, IEnumerable<ArraySegment<byte>>> callback);
        void SendAsync(IEnumerable<ArraySegment<byte>> data);
        void Close();
        void Close(SocketError socketError, string reason);
    }
}