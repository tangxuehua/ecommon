using System;
using System.Collections.Concurrent;
using ECommon.Logging;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace ECommon.Serilog
{
    public class SerilogLoggerFactory : ILoggerFactory
    {
        private readonly string _defaultLoggerName;
        private readonly ConcurrentDictionary<string, SerilogLogger> _loggerDict = new ConcurrentDictionary<string, SerilogLogger>();

        public SerilogLoggerFactory(string defaultLoggerName = "default", string defaultLoggerFileName = "default", LogEventLevel consoleMinimumLevel = LogEventLevel.Information, LogEventLevel fileMinimumLevel = LogEventLevel.Information)
        {
            _defaultLoggerName = defaultLoggerName;
            _loggerDict.TryAdd(defaultLoggerName, new SerilogLogger("logger", new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.Console(
                    restrictedToMinimumLevel: consoleMinimumLevel,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] - {Message:lj}{NewLine}{Exception}")
                .WriteTo.File(defaultLoggerFileName + "-.txt",
                    restrictedToMinimumLevel: fileMinimumLevel,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{logger}] - {Message:lj}{NewLine}{Exception}",
                    buffered: true,
                    fileSizeLimitBytes: null,
                    rollingInterval: RollingInterval.Day,
                    rollOnFileSizeLimit: true,
                    retainedFileCountLimit: null,
                    flushToDiskInterval: new TimeSpan(0, 0, 1))
                .CreateLogger()));
        }
        public SerilogLoggerFactory AddFileLogger(string loggerName, string loggerFileName, LogEventLevel minimumLevel = LogEventLevel.Information)
        {
            _loggerDict.TryAdd(loggerName, new SerilogLogger("logger", new LoggerConfiguration()
                .MinimumLevel.Verbose()
                .WriteTo.File(loggerFileName + "-.txt",
                    restrictedToMinimumLevel: minimumLevel,
                    outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{logger}] - {Message:lj}{NewLine}{Exception}",
                    buffered: true,
                    fileSizeLimitBytes: null,
                    rollingInterval: RollingInterval.Day,
                    rollOnFileSizeLimit: true,
                    retainedFileCountLimit: null,
                    flushToDiskInterval: new TimeSpan(0, 0, 1))
                .CreateLogger()));
            return this;
        }
        public SerilogLoggerFactory AddFileLogger(string loggerName, string contextPropertyName, Logger logger)
        {
            _loggerDict.TryAdd(loggerName, new SerilogLogger(contextPropertyName, logger));
            return this;
        }
        public SerilogLoggerFactory AddFileLogger(string loggerName, SerilogLogger logger)
        {
            _loggerDict.TryAdd(loggerName, logger);
            return this;
        }

        public Logging.ILogger Create(string name)
        {
            return GetLogger(name);
        }

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