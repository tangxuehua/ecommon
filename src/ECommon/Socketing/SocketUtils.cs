using System.Linq;
using System.Net;
using System.Net.Sockets;
using ECommon.Utilities;

namespace ECommon.Socketing
{
    public class SocketUtils
    {
        public static IPAddress GetLocalIPV4()
        {
            return Dns.GetHostEntry(Dns.GetHostName()).AddressList.First(x => x.AddressFamily == AddressFamily.InterNetwork);
        }
        public static Socket CreateSocket()
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.ReceiveBufferSize = 8192;
            socket.NoDelay = true;
            socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontLinger, true);
            return socket;
        }
        public static void ShutdownSocket(Socket socket)
        {
            if (socket == null) return;

            Helper.EatException(() => socket.Shutdown(SocketShutdown.Both));
            Helper.EatException(() => socket.Close(10000));
        }
        public static void CloseSocket(Socket socket)
        {
            if (socket == null) return;

            Helper.EatException(() => socket.Close(10000));
        }
    }
}
