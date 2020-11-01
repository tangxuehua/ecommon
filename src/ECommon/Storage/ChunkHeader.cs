using System;
using System.IO;
using ECommon.Utilities;

namespace ECommon.Storage
{
    public class ChunkHeader
    {
        public const int Size = 128;
        public readonly int ChunkNumber;
        public readonly int ChunkDataTotalSize;
        public readonly long ChunkDataStartPosition;
        public readonly long ChunkDataEndPosition;
        public readonly int BloomFilterSize;

        public ChunkHeader(int chunkNumber, int chunkDataTotalSize, int bloomFilterSize)
        {
            Ensure.Nonnegative(chunkNumber, "chunkNumber");
            Ensure.Positive(chunkDataTotalSize, "chunkDataTotalSize");

            ChunkNumber = chunkNumber;
            ChunkDataTotalSize = chunkDataTotalSize;
            BloomFilterSize = bloomFilterSize;
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
                    writer.Write(BloomFilterSize);
                }
            }
            return array;
        }
        public ChunkHeader FromStream(BinaryReader reader)
        {
            var chunkNumber = reader.ReadInt32();
            var chunkDataTotalSize = reader.ReadInt32();
            var bloomFilterSize = reader.ReadInt32();
            return new ChunkHeader(chunkNumber, chunkDataTotalSize, bloomFilterSize);
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
            return string.Format("[ChunkNumber:{0}, ChunkDataTotalSize:{1}, ChunkDataStartPosition:{2}, ChunkDataEndPosition:{3}, BloomFilterSize:{4}]",
                                 ChunkNumber,
                                 ChunkDataTotalSize,
                                 ChunkDataStartPosition,
                                 ChunkDataEndPosition,
                                 BloomFilterSize);
        }
    }
}
