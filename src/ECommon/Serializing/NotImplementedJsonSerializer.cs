using System;

namespace ECommon.Serializing
{
    public class NotImplementedJsonSerializer : IJsonSerializer
    {
        public string Serialize(object obj)
        {
            throw new NotSupportedException(string.Format("{0} does not support serializing object.", GetType().FullName));
        }
        public object Deserialize(string value, Type type)
        {
            throw new NotSupportedException(string.Format("{0} does not support deserializing object.", GetType().FullName));
        }
        public T Deserialize<T>(string value) where T : class
        {
            throw new NotSupportedException(string.Format("{0} does not support deserializing object.", GetType().FullName));
        }
    }
}
