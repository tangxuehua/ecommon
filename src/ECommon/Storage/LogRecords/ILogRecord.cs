using System.IO;

namespace ECommon.Storage.LogRecords
{
    public interface ILogRecord
    {
        void WriteTo(long logPosition, BinaryWriter writer);
        void ReadFrom(byte[] recordBuffer);
    }
}
