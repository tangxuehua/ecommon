using System;
using System.IO;
using System.Text;
using ECommon.Utilities;

namespace ECommon.Storage
{
    public class ChunkBloomFilter
    {
        public readonly int Size;
        public readonly int Version = 1;
        public readonly string MinKey;
        public readonly string MaxKey;
        public readonly byte[] BloomFilterBytes;

        public ChunkBloomFilter(int size, string minKey, string maxKey, byte[] bloomFilterBytes) : this(size, 1, minKey, maxKey, bloomFilterBytes) { }
        public ChunkBloomFilter(int size, int version, string minKey, string maxKey, byte[] bloomFilterBytes)
        {
            Ensure.Positive(size, "size");
            Ensure.Positive(version, "version");
            Ensure.NotNullOrEmpty(minKey, "minKey");
            Ensure.NotNullOrEmpty(maxKey, "maxKey");
            Ensure.NotNull(bloomFilterBytes, "bloomFilterBytes");
            if (bloomFilterBytes.Length <= 0)
            {
                throw new ArgumentException("bloomFilterBytes should not be empty.");
            }

            var minKeyBytes = Encoding.UTF8.GetBytes(minKey);
            var maxKeyBytes = Encoding.UTF8.GetBytes(maxKey);
            int contentSize = sizeof(int) + sizeof(int) + sizeof(int) + minKeyBytes.Length + sizeof(int) + maxKeyBytes.Length + sizeof(int) + bloomFilterBytes.Length;

            if (contentSize > size)
            {
                throw new ArgumentException(string.Format("Bloom filter content size '{0}' is more than the max size: {1}", contentSize, size));
            }

            Size = size;
            Version = version;
            MinKey = minKey;
            MaxKey = maxKey;
            BloomFilterBytes = bloomFilterBytes;
        }

        public byte[] AsByteArray()
        {
            var array = new byte[Size];

            using (var stream = new MemoryStream(array))
            {
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(Size);
                    writer.Write(Version);
                    writer.Write(MinKey);
                    writer.Write(MaxKey);
                    writer.Write(BloomFilterBytes.Length);
                    writer.Write(BloomFilterBytes);
                }
            }

            return array;
        }

        public static ChunkBloomFilter FromStream(BinaryReader reader)
        {
            long originalPosition = reader.BaseStream.Position;
            var size = reader.ReadInt32();
            var version = reader.ReadInt32();
            var minKey = reader.ReadString();
            var maxKey = reader.ReadString();
            var bloomFilterBytesLength = reader.ReadInt32();
            var bloomFilterBytes = reader.ReadBytes(bloomFilterBytesLength);
            reader.BaseStream.Position = originalPosition + size;

            return new ChunkBloomFilter(size, version, minKey, maxKey, bloomFilterBytes);
        }
    }
}
