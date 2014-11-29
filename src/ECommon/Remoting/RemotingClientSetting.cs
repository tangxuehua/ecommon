using System.Net;

namespace ECommon.Remoting
{
    public class RemotingClientSetting
    {
        public int ReconnectInterval;
        public IPEndPoint LocalEndPoint;

        public RemotingClientSetting()
        {
            ReconnectInterval = 1000;
        }
    }
}
