using System;
using System.Net.Sockets;

namespace ECommon.Socketing
{
    public interface ISocketServerEventListener
    {
        void OnConnectionAccepted(ITcpConnection connection);
        void OnConnectionClosed(ITcpConnection connection, SocketError socketError);
        void OnMessageArrived(ITcpConnection connection, byte[] message, Action<byte[]> sendReplyAction);
    }
}
