using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace ECommon.Socketing
{
    public class SocketUtils
    {
        public static string GetLocalIPV4()
        {
            return Dns.GetHostEntry(Dns.GetHostName()).AddressList.First(x => x.AddressFamily == AddressFamily.InterNetwork).ToString();
        }
        public static bool IsSocketDisconnectedException(SocketException socketException)
        {
            var errorCode = socketException.SocketErrorCode;
            return errorCode == SocketError.NetworkDown ||
                   errorCode == SocketError.NetworkReset ||
                   errorCode == SocketError.NetworkUnreachable ||
                   errorCode == SocketError.ConnectionAborted ||
                   errorCode == SocketError.ConnectionReset ||
                   errorCode == SocketError.HostNotFound ||
                   errorCode == SocketError.HostUnreachable ||
                   errorCode == SocketError.NotConnected ||
                   errorCode == SocketError.Shutdown ||
                   errorCode == SocketError.Disconnecting ||
                   errorCode == SocketError.AddressNotAvailable ||
                   errorCode == SocketError.TimedOut;
        }
        public static int ParseMessageLength(byte[] buffer)
        {
            var data = new byte[4];
            for (var i = 0; i < 4; i++)
            {
                data[i] = buffer[i];
            }
            return BitConverter.ToInt32(data, 0);
        }
        public static byte[] BuildMessage(byte[] data)
        {
            var header = BitConverter.GetBytes(data.Length);
            var message = new byte[header.Length + data.Length];
            header.CopyTo(message, 0);
            data.CopyTo(message, header.Length);
            return message;
        }
    }
}
