using System;
using System.IO;
using System.Runtime.Serialization;
using ECommon.Serializing;
using ProtoBuf;

namespace ECommon.ProtocolBuf
{
    public class ProtocolBufSerializer : IBinarySerializer
    {
        public byte[] Serialize(object obj)
        {
            using (var stream = new MemoryStream())
            {
                Serializer.Serialize(stream, obj);
                return stream.ToArray();
            }
        }
        public T Deserialize<T>(byte[] data) where T : class
        {
            using (var stream = new MemoryStream(data))
            {
                var instance = FormatterServices.GetUninitializedObject(typeof(T)) as T;
                Serializer.Merge(stream, instance);
                return instance;
            }
        }
        public object Deserialize(byte[] data, Type type)
        {
            using (var stream = new MemoryStream(data))
            {
                var instance = FormatterServices.GetUninitializedObject(type);
                Serializer.NonGeneric.Merge(stream, instance);
                return instance;
            }
        }
    }
}
