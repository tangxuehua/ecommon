using System;
using System.Net.Sockets;

namespace ECommon.Socketing
{
    public class SocketInfo
    {
        public string SocketRemotingEndpointAddress { get; private set; }
        public Socket InnerSocket { get; private set; }

        public SocketInfo(Socket socket)
        {
            if (!socket.Connected)
            {
                throw new Exception("Invalid socket, socket is disconnected.");
            }
            InnerSocket = socket;
            SocketRemotingEndpointAddress = socket.RemoteEndPoint.ToString();
        }

        public void Close()
        {
            if (InnerSocket == null || !InnerSocket.Connected) return;

            try
            {
                InnerSocket.Shutdown(SocketShutdown.Both);
            }
            catch { }
            try
            {
                InnerSocket.Close();
            }
            catch
            { }
        }
    }
}
