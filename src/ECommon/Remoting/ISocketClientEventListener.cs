using System.Net.Sockets;
using ECommon.TcpTransport;

namespace ECommon.Socketing
{
    public interface ISocketClientEventListener
    {
        void OnConnectionEstablished(ITcpConnectionInfo connectionInfo);
        void OnConnectionFailed(ITcpConnectionInfo connectionInfo, SocketError socketError);
        void OnConnectionClosed(ITcpConnectionInfo connectionInfo, SocketError socketError);
    }
}
