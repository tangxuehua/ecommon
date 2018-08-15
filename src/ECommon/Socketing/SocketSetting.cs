namespace ECommon.Socketing
{
    public class SocketSetting
    {
        public int SendBufferSize = 1024 * 64;
        public int ReceiveBufferSize = 1024 * 64;

        public int MaxSendPacketSize = 1024 * 64;
        public int SendMessageFlowControlThreshold = 1000;

        public int ReconnectToServerInterval = 1000;
        public int ScanTimeoutRequestInterval = 1000;

        public int ReceiveDataBufferSize = 1024 * 64;
        public int ReceiveDataBufferPoolSize = 50;

        public int SendHeartbeatInterval = 1000;
        public int HeartbeatResponseTimeoutMilliseconds = 5000;
    }
}
