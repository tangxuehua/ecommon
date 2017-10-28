using System;
using System.IO;
using System.Text;

namespace ECommon.Extensions
{
    public static class BinaryWriterExtensions
    {
        public static BinaryWriter WriteString(this BinaryWriter writer, string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            writer.Write(bytes.Length);
            writer.Write(bytes);
            return writer;
        }
        public static BinaryWriter WriteInt(this BinaryWriter writer, int value)
        {
            writer.Write(value);
            return writer;
        }
        public static BinaryWriter WriteLong(this BinaryWriter writer, long value)
        {
            writer.Write(value);
            return writer;
        }
        public static BinaryWriter WriteDateTime(this BinaryWriter writer, DateTime value)
        {
            writer.Write(value.Ticks);
            return writer;
        }
    }
}
