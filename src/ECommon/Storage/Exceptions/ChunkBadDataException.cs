using System;

namespace ECommon.Storage.Exceptions
{
    public class ChunkBadDataException : Exception
    {
        public ChunkBadDataException(string message) : base(message)
        {
        }
    }
}
