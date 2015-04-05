using ECommon.TcpTransport;

namespace ECommon.Configurations
{
    public class Setting
    {
        public TcpConfiguration TcpConfiguration { get; set; }

        public Setting()
        {
            TcpConfiguration = new TcpConfiguration();
        }
    }
}
