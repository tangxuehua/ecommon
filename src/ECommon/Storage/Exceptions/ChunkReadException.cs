using System;

namespace ECommon.Storage.Exceptions
{
    public class ChunkReadException : Exception
    {
        public ChunkReadException(string message) : base(message) { }
    }
}
