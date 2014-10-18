using System;

namespace ECommon.TcpTransport.BufferManagement
{
    public class UnableToAllocateBufferException : Exception
    {
        public UnableToAllocateBufferException()
            : base("Couldn't allocate buffer after few trials.")
        {
        }
    }
}
