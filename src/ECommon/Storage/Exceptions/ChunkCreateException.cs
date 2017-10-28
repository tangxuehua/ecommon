using System;

namespace ECommon.Storage.Exceptions
{
    public class ChunkCreateException : Exception
    {
        public ChunkCreateException(string message) : base(message) { }
    }
}
