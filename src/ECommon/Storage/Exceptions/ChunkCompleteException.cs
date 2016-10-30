using System;

namespace ECommon.Storage.Exceptions
{
    public class ChunkCompleteException : Exception
    {
        public ChunkCompleteException(string message) : base(message) { }
    }
}
