using System;
using System.Net.Sockets;

namespace ECommon.Socketing
{
    public interface ISocketEventListener
    {
        void OnNewSocketAccepted(SocketInfo socketInfo);
        void OnSocketDisconnected(SocketInfo socketInfo);
        void OnSocketReceiveException(SocketInfo socketInfo, Exception exception);
    }
}
