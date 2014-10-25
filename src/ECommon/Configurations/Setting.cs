using ECommon.TcpTransport;

namespace ECommon.Configurations
{
    public class Setting
    {
        public int RetryActionDefaultPeriod { get; set; }
        public TcpConfiguration TcpConfiguration { get; set; }

        public Setting()
        {
            RetryActionDefaultPeriod = 1000;
            TcpConfiguration = new TcpConfiguration();
        }
    }
}
