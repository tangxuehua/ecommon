using System;

namespace ECommon.Socketing.BufferManagement
{
    public class UnableToAllocateBufferException : Exception
    {
        public UnableToAllocateBufferException()
            : base("Couldn't allocate buffer after few trials.")
        {
        }
    }
}
