using System;
using System.Net.Sockets;

namespace ECommon.Socketing
{
    public class SendContext
    {
        public SocketInfo TargetSocket { get; private set; }
        public byte[] Message { get; private set; }
        public Action<SendResult> MessageSendCallback { get; private set; }

        public SendContext(SocketInfo targetSocket, byte[] message, Action<SendResult> messageSendCallback)
        {
            TargetSocket = targetSocket;
            Message = message;
            MessageSendCallback = messageSendCallback;
        }
    }
}
