namespace ECommon.Socketing
{
    public class SocketSetting
    {
        public int MaxSendPacketSize = 1024 * 64;
        public int SendMessageFlowControlCount = 500000;
        public int SendMessageFlowControlWaitMilliseconds = 5;

        public int ReceiveDataBufferSize = 1024 * 8;
        public int ReceiveDataBufferCount = 50;
    }
}
