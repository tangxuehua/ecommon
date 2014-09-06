using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ECommon.Socketing
{
    public class SocketUtils
    {
        public const int MessageHeaderLength = 6;

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
        public static int ParseMessageLength(byte[] buffer, out string errorMessage)
        {
            errorMessage = null;
            var data1 = new byte[2];
            for (var i = 0; i < 2; i++)
            {
                data1[i] = buffer[i];
            }
            var flag = Encoding.UTF8.GetString(data1);
            if (flag != "S:")
            {
                errorMessage = "Invalid message header flag:" + flag;
                return -1;
            }

            var data2 = new byte[4];
            for (var i = 2; i < 6; i++)
            {
                data2[i - 2] = buffer[i];
            }
            var length = BitConverter.ToInt32(data2, 0);
            if (length <= 0)
            {
                errorMessage = "Invalid message length:" + length;
                return -1;
            }

            return length;
        }
        public static byte[] BuildMessage(byte[] data)
        {
            var flag = Encoding.UTF8.GetBytes("S:");
            var header = BitConverter.GetBytes(data.Length);
            var message = new byte[flag.Length + header.Length + data.Length];
            flag.CopyTo(message, 0);
            header.CopyTo(message, flag.Length);
            data.CopyTo(message, flag.Length + header.Length);
            return message;
        }
    }
}
