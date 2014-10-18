using System.Linq;
using System.Net;
using System.Net.Sockets;

namespace ECommon.Utilities
{
    public class SocketUtils
    {
        public static IPAddress GetLocalIPV4()
        {
            return Dns.GetHostEntry(Dns.GetHostName()).AddressList.First(x => x.AddressFamily == AddressFamily.InterNetwork);
        }
    }
}
