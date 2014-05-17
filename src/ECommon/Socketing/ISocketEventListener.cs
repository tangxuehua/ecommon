using System.Net.Sockets;

namespace ECommon.Socketing
{
    public interface ISocketEventListener
    {
        void OnNewSocketAccepted(SocketInfo socketInfo);
        void OnSocketException(SocketInfo socketInfo, SocketException socketException);
    }
}
