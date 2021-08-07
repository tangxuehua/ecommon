using System.IO;
using ECommon.Utilities;

namespace ECommon.Storage
{
    public class ChunkFooter
    {
        public const int Size = 128;
        public readonly int ChunkFilterTotalSize;
        public readonly int ChunkDataTotalSize;

        public ChunkFooter(int chunkFilterTotalSize, int chunkDataTotalSize)
        {
            Ensure.Nonnegative(chunkFilterTotalSize, "chunkFilterTotalSize");
            Ensure.Nonnegative(chunkDataTotalSize, "chunkDataTotalSize");
            ChunkFilterTotalSize = chunkFilterTotalSize;
            ChunkDataTotalSize = chunkDataTotalSize;
        }

        public byte[] AsByteArray()
        {
            var array = new byte[Size];
            using (var stream = new MemoryStream(array))
            {
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(ChunkDataTotalSize);
                    writer.Write(ChunkFilterTotalSize);
                }
            }
            return array;
        }

        public static ChunkFooter FromStream(BinaryReader reader)
        {
            var chunkDataTotalSize = reader.ReadInt32();
            var chunkFilterTotalSize = reader.ReadInt32();
            return new ChunkFooter(chunkFilterTotalSize, chunkDataTotalSize);
        }

        public override string ToString()
        {
            return string.Format("[ChunkDataTotalSize:{0},ChunkFilterTotalSize:{1}]", ChunkDataTotalSize, ChunkFilterTotalSize);
        }
    }
}
