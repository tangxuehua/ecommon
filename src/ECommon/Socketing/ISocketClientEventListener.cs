using System.Net;
using System.Net.Sockets;

namespace ECommon.Socketing
{
    public interface ISocketClientEventListener
    {
        void OnConnectionEstablished(ITcpConnection connection);
        void OnConnectionClosed(ITcpConnection connection, SocketError socketError);
        void OnMessageArrived(ITcpConnection connection, byte[] message);
    }
}
