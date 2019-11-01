using ECommon.Serilog;
using ECommon.Logging;
using Serilog.Events;

namespace ECommon.Configurations
{
    /// <summary>ENode configuration class Autofac extensions.
    /// </summary>
    public static class ConfigurationExtensions
    {
        /// <summary>Use Serilog as the logger.
        /// </summary>
        /// <returns></returns>
        public static Configuration UseSerilog(this Configuration configuration, string defaultLoggerName = "default", string defaultLoggerFileName = "default", LogEventLevel consoleMinimumLevel = LogEventLevel.Information, LogEventLevel fileMinimumLevel = LogEventLevel.Information)
        {
            return UseSerilog(configuration, new SerilogLoggerFactory(defaultLoggerName, defaultLoggerFileName, consoleMinimumLevel, fileMinimumLevel));
        }
        /// <summary>Use Serilog as the logger.
        /// </summary>
        /// <returns></returns>
        public static Configuration UseSerilog(this Configuration configuration, SerilogLoggerFactory loggerFactory)
        {
            configuration.SetDefault<ILoggerFactory, SerilogLoggerFactory>(loggerFactory);
            return configuration;
        }
    }
}