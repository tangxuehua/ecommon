using System;
using ECommon.Configurations;
using ECommon.Serializing;

namespace ECommon.JsonNet
{
    /// <summary>ECommon configuration class JsonNet extensions.
    /// </summary>
    public static class ConfigurationExtensions
    {
        /// <summary>Use Json.Net as the json serializer.
        /// </summary>
        /// <returns></returns>
        public static Configuration UseJsonNet(this Configuration configuration)
        {
            configuration.SetDefault<IJsonSerializer, NewtonsoftJsonSerializer>(new NewtonsoftJsonSerializer());
            return configuration;
        }
    }
}
