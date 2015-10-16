namespace ECommon.Socketing
{
    public class SocketSetting
    {
        public int SendBufferSize = 1024 * 16;
        public int ReceiveBufferSize = 1024 * 16;

        public int MaxSendPacketSize = 1024 * 64;
        public int SendMessageFlowControlThreshold = 50000;
        public int SendMessageFlowControlWaitMilliseconds = 5;

        public int ReceiveDataBufferSize = 1024 * 16;
        public int ReceiveDataBufferPoolSize = 50;
    }
}
