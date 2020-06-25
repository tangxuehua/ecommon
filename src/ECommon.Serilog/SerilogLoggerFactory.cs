using System;
using System.Collections.Concurrent;
using ECommon.Logging;
using Serilog;
using Serilog.Events;

namespace ECommon.Serilog
{
    /// <summary>The Serilog implementation of ILoggerFactory.
    /// </summary>
    public class SerilogLoggerFactory : ILoggerFactory
    {
        private readonly string _defaultLoggerName;
        private readonly ConcurrentDictionary<string, SerilogLogger> _loggerDict = new ConcurrentDictionary<string, SerilogLogger>();

        /// <summary>Default constructor
        /// </summary>
        /// <param name="defaultLoggerName"></param>
        /// <param name="defaultLoggerFileName"></param>
        /// <param name="defaultLoggerFileExtensions"></param>
        /// <param name="contextPropertyName"></param>
        /// <param name="defaultLoggerConsoleOutputTemplate"></param>
        /// <param name="defaultLoggerFileOutputTemplate"></param>
        /// <param name="defaultLoggerFileBuffered"></param>
        /// <param name="defaultLoggerFileSizeLimitBytes"></param>
        /// <param name="defaultLoggerFileRollingInterval"></param>
        /// <param name="defaultLoggerFileRollOnFileSizeLimit"></param>
        /// <param name="defaultLoggerFileRetainedFileCountLimit"></param>
        /// <param name="defaultLoggerFileFlushToDiskIntervalSenconds"></param>
        /// <param name="consoleMinimumLevel"></param>
        /// <param name="fileMinimumLevel"></param>
        public SerilogLoggerFactory(
            string defaultLoggerName = "default",
            string defaultLoggerFileName = "default",
            string defaultLoggerFileExtensions = "-.log",
            string contextPropertyName = "logger",
            string defaultLoggerConsoleOutputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] - {Message:lj}{NewLine}{Exception}",
            string defaultLoggerFileOutputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{logger}] - {Message:lj}{NewLine}{Exception}",
            bool defaultLoggerFileBuffered = true,
            long? defaultLoggerFileSizeLimitBytes = null,
            RollingInterval defaultLoggerFileRollingInterval = RollingInterval.Day,
            bool defaultLoggerFileRollOnFileSizeLimit = true,
            int? defaultLoggerFileRetainedFileCountLimit = null,
            int? defaultLoggerFileFlushToDiskIntervalSenconds = 1,
            LogEventLevel consoleMinimumLevel = LogEventLevel.Information,
            LogEventLevel fileMinimumLevel = LogEventLevel.Information)
        {
            var defaultLoggerFileFlushToDiskInterval = defaultLoggerFileFlushToDiskIntervalSenconds != null ? new TimeSpan(defaultLoggerFileFlushToDiskIntervalSenconds.Value * 1000 * 10000) : default(TimeSpan?);
            _defaultLoggerName = defaultLoggerName;
            _loggerDict.TryAdd(defaultLoggerName, new SerilogLogger(contextPropertyName, new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.Console(
                    restrictedToMinimumLevel: consoleMinimumLevel,
                    outputTemplate: defaultLoggerConsoleOutputTemplate)
                .WriteTo.File(defaultLoggerFileName + defaultLoggerFileExtensions,
                    restrictedToMinimumLevel: fileMinimumLevel,
                    outputTemplate: defaultLoggerFileOutputTemplate,
                    buffered: defaultLoggerFileBuffered,
                    fileSizeLimitBytes: defaultLoggerFileSizeLimitBytes,
                    rollingInterval: defaultLoggerFileRollingInterval,
                    rollOnFileSizeLimit: defaultLoggerFileRollOnFileSizeLimit,
                    retainedFileCountLimit: defaultLoggerFileRetainedFileCountLimit,
                    flushToDiskInterval: defaultLoggerFileFlushToDiskInterval)
                .CreateLogger()));
        }
        /// <summary>Add a file logger.
        /// </summary>
        /// <param name="loggerName"></param>
        /// <param name="loggerFileName"></param>
        /// <param name="contextPropertyName"></param>
        /// <param name="defaultLoggerFileExtensions"></param>
        /// <param name="defaultLoggerFileOutputTemplate"></param>
        /// <param name="defaultLoggerFileBuffered"></param>
        /// <param name="defaultLoggerFileSizeLimitBytes"></param>
        /// <param name="defaultLoggerFileRollingInterval"></param>
        /// <param name="defaultLoggerFileRollOnFileSizeLimit"></param>
        /// <param name="defaultLoggerFileRetainedFileCountLimit"></param>
        /// <param name="defaultLoggerFileFlushToDiskIntervalSenconds"></param>
        /// <param name="minimumLevel"></param>
        /// <returns></returns>
        public SerilogLoggerFactory AddFileLogger(string loggerName,
            string loggerFileName,
            string contextPropertyName = "logger",
            string defaultLoggerFileExtensions = "-.log",
            string defaultLoggerFileOutputTemplate = "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{logger}] - {Message:lj}{NewLine}{Exception}",
            bool defaultLoggerFileBuffered = true,
            long? defaultLoggerFileSizeLimitBytes = null,
            RollingInterval defaultLoggerFileRollingInterval = RollingInterval.Day,
            bool defaultLoggerFileRollOnFileSizeLimit = true,
            int? defaultLoggerFileRetainedFileCountLimit = null,
            int? defaultLoggerFileFlushToDiskIntervalSenconds = 1,
            LogEventLevel minimumLevel = LogEventLevel.Information)
        {
            var defaultLoggerFileFlushToDiskInterval = defaultLoggerFileFlushToDiskIntervalSenconds != null ? new TimeSpan(defaultLoggerFileFlushToDiskIntervalSenconds.Value * 1000 * 10000) : default(TimeSpan?);
            _loggerDict.TryAdd(loggerName, new SerilogLogger(contextPropertyName, new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.File(loggerFileName + defaultLoggerFileExtensions,
                    restrictedToMinimumLevel: minimumLevel,
                    outputTemplate: defaultLoggerFileOutputTemplate,
                    buffered: defaultLoggerFileBuffered,
                    fileSizeLimitBytes: defaultLoggerFileSizeLimitBytes,
                    rollingInterval: defaultLoggerFileRollingInterval,
                    rollOnFileSizeLimit: defaultLoggerFileRollOnFileSizeLimit,
                    retainedFileCountLimit: defaultLoggerFileRetainedFileCountLimit,
                    flushToDiskInterval: defaultLoggerFileFlushToDiskInterval)
                .CreateLogger()));
            return this;
        }
        /// <summary>Add a file logger.
        /// </summary>
        /// <param name="loggerName"></param>
        /// <param name="logger"></param>
        /// <returns></returns>
        public SerilogLoggerFactory AddFileLogger(string loggerName, SerilogLogger logger)
        {
            _loggerDict.TryAdd(loggerName, logger);
            return this;
        }

        /// <summary>Create a new logger.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public Logging.ILogger Create(string name)
        {
            return GetLogger(name);
        }
        /// <summary>Create a new logger.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public Logging.ILogger Create(Type type)
        {
            return GetLogger(type.FullName);
        }

        private Logging.ILogger GetLogger(string name)
        {
            if (_loggerDict.TryGetValue(name, out SerilogLogger logger))
            {
                return logger;
            }

            string foundLoggerName = null;
            foreach(var loggerName in _loggerDict.Keys)
            {
                if (name.StartsWith(loggerName))
                {
                    if (foundLoggerName == null)
                    {
                        foundLoggerName = loggerName;
                    }
                    else if (loggerName.Length > foundLoggerName.Length)
                    {
                        foundLoggerName = loggerName;
                    }
                }
            }

            if (foundLoggerName != null)
            {
                if (_loggerDict.TryGetValue(foundLoggerName, out SerilogLogger foundLogger))
                {
                    var newLogger = new SerilogLogger(foundLogger.ContextPropertyName, foundLogger.SerilogILogger.ForContext(foundLogger.ContextPropertyName, name));
                    _loggerDict.TryAdd(name, newLogger);
                    return newLogger;
                }
            }

            if (_loggerDict.TryGetValue(_defaultLoggerName, out SerilogLogger defaultLogger))
            {
                var newLogger = new SerilogLogger(defaultLogger.ContextPropertyName, defaultLogger.SerilogILogger.ForContext(defaultLogger.ContextPropertyName, name));
                _loggerDict.TryAdd(name, newLogger);
                return newLogger;
            }

            return null;
        }
    }
}