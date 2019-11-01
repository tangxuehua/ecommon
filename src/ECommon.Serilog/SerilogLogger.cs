using System;
using SerilogILogger = Serilog.ILogger;
using LogLevel = Serilog.Events.LogEventLevel;

namespace ECommon.Serilog
{
    public class SerilogLogger : Logging.ILogger
    {
        private readonly object _lockObj = new object();
        public string ContextPropertyName { get; }
        public SerilogILogger SerilogILogger { get; }

        public SerilogLogger(string contextPropertyName, SerilogILogger logger)
        {
            ContextPropertyName = contextPropertyName;
            SerilogILogger = logger;
        }

        public bool IsDebugEnabled => SerilogILogger.IsEnabled(LogLevel.Debug);

        public void Debug(string message)
        {
            SerilogILogger.Debug(message);
        }

        public void Debug(string message, Exception exception)
        {
            SerilogILogger.Debug(exception, message);
        }

        public void DebugFormat(string format, params object[] args)
        {
            SerilogILogger.Debug(format, args);
        }

        public void Error(string message)
        {
            SerilogILogger.Error(message);
        }

        public void Error(string message, Exception exception)
        {
            SerilogILogger.Error(exception, message);
        }

        public void ErrorFormat(string format, params object[] args)
        {
            SerilogILogger.Error(format, args);
        }

        public void Fatal(string message)
        {
            SerilogILogger.Fatal(message);
        }

        public void Fatal(string message, Exception exception)
        {
            SerilogILogger.Fatal(message, exception);
        }

        public void FatalFormat(string format, params object[] args)
        {
            SerilogILogger.Fatal(format, args);
        }

        public void Info(string message)
        {
            SerilogILogger.Information(message);
        }

        public void Info(string message, Exception exception)
        {
            SerilogILogger.Information(exception, message);
        }

        public void InfoFormat(string format, params object[] args)
        {
            SerilogILogger.Information(format, args);
        }

        public void Warn(string message)
        {
            SerilogILogger.Warning(message);
        }

        public void Warn(string message, Exception exception)
        {
            SerilogILogger.Warning(message, exception);
        }

        public void WarnFormat(string format, params object[] args)
        {
            SerilogILogger.Warning(format, args);
        }
    }
}
