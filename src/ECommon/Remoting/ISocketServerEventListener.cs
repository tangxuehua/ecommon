using System.Net.Sockets;
using ECommon.TcpTransport;

namespace ECommon.Socketing
{
    public interface ISocketServerEventListener
    {
        void OnConnectionAccepted(ITcpConnectionInfo connectionInfo);
        void OnConnectionClosed(ITcpConnectionInfo connectionInfo, SocketError socketError);
    }
}
