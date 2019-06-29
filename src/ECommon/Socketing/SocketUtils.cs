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
           return  System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                  .Select(p => p.GetIPProperties())
                  .SelectMany(p => p.UnicastAddresses)
                  .Where(p => p.Address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(p.Address))
                  .FirstOrDefault()?.Address;
        }
        public static Socket CreateSocket(int sendBufferSize, int receiveBufferSize)
        {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                NoDelay = true,
                Blocking = false,
                SendBufferSize = sendBufferSize,
                ReceiveBufferSize = receiveBufferSize
            };
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
