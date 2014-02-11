using ECommon.Configurations;
using ECommon.Serializing;

namespace ECommon.ProtocolBuf
{
    /// <summary>ECommon configuration class ProtocolBuf extensions.
    /// </summary>
    public static class ConfigurationExtensions
    {
        /// <summary>Use ProtocolBufSerializer as the binary serializer.
        /// </summary>
        /// <returns></returns>
        public static Configuration UseProtoBufSerializer(this Configuration configuration)
        {
            configuration.SetDefault<IBinarySerializer, ProtocolBufSerializer>(new ProtocolBufSerializer());
            return configuration;
        }
    }
}
