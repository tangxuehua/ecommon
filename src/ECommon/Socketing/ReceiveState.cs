using System;
using System.Collections.Generic;
using System.Net.Sockets;

namespace ECommon.Socketing
{
    public class ReceiveState
    {
        public const int BufferSize = 8192;
        public byte[] Buffer = new byte[BufferSize];
        public int ReceiveSize { get; set; }
        public List<byte> ReceivedData = new List<byte>();
        public int? MessageSize;
        public SocketInfo SourceSocket { get; private set; }
        public Action<byte[]> MessageReceivedCallback { get; private set; }
        public void ClearBuffer()
        {
            for (var index = 0; index < BufferSize; index++)
            {
                Buffer[index] = 0;
            }
        }

        public ReceiveState(SocketInfo sourceSocket, int receiveSize, Action<byte[]> messageReceivedCallback)
        {
            SourceSocket = sourceSocket;
            ReceiveSize = receiveSize;
            MessageReceivedCallback = messageReceivedCallback;
        }
    }
}
