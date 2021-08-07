using System;
using System.IO;
using ECommon.Utilities;

namespace ECommon.Storage
{
    public enum ChunkType
    {
        Default = 0,
        Index
    }
    public class ChunkHeader
    {
        public const int Size = 128;
        public readonly int ChunkNumber;
        public ChunkType ChunkType;
        public readonly int Version;
        public readonly int ChunkDataTotalSize;
        public readonly long ChunkDataStartPosition;
        public readonly long ChunkDataEndPosition;

        public ChunkHeader(int chunkNumber, int chunkDataTotalSize, ChunkType chunkType = ChunkType.Default, int version = 0)
        {
            Ensure.Nonnegative(chunkNumber, "chunkNumber");
            Ensure.Positive(chunkDataTotalSize, "chunkDataTotalSize");

            ChunkNumber = chunkNumber;
            ChunkDataTotalSize = chunkDataTotalSize;
            ChunkType = chunkType;
            Version = version;
            ChunkDataStartPosition = ChunkNumber * (long)ChunkDataTotalSize;
            ChunkDataEndPosition = (ChunkNumber + 1) * (long)ChunkDataTotalSize;
        }

        public byte[] AsByteArray()
        {
            var array = new byte[Size];
            using (var stream = new MemoryStream(array))
            {
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(ChunkNumber);
                    writer.Write(ChunkDataTotalSize);
                    writer.Write(Convert.ToInt32(ChunkType));
                    writer.Write(Version);
                }
            }
            return array;
        }
        public static ChunkHeader FromStream(BinaryReader reader)
        {
            var chunkNumber = reader.ReadInt32();
            var chunkDataTotalSize = reader.ReadInt32();
            var chunkType = reader.ReadInt32();
            var version = reader.ReadInt32();
            return new ChunkHeader(chunkNumber, chunkDataTotalSize, (ChunkType)chunkType, version);
        }

        public int GetLocalDataPosition(long globalDataPosition)
        {
            if (globalDataPosition < ChunkDataStartPosition || globalDataPosition > ChunkDataEndPosition)
            {
                throw new Exception(string.Format("globalDataPosition {0} is out of chunk data positions [{1}, {2}].", globalDataPosition, ChunkDataStartPosition, ChunkDataEndPosition));
            }
            return (int)(globalDataPosition - ChunkDataStartPosition);
        }

        public override string ToString()
        {
            return string.Format("[ChunkNumber:{0}, ChunkType:{1}, Version:{2}, ChunkDataTotalSize:{3}, ChunkDataStartPosition:{4}, ChunkDataEndPosition:{5}]",
                                 ChunkNumber,
                                 ChunkType,
                                 Version,
                                 ChunkDataTotalSize,
                                 ChunkDataStartPosition,
                                 ChunkDataEndPosition);
        }
    }
}
