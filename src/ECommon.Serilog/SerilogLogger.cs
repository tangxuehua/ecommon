using System;
using SerilogILogger = Serilog.ILogger;
using LogLevel = Serilog.Events.LogEventLevel;

namespace ECommon.Serilog
{
    /// <summary>Represents a serilog logger.
    /// </summary>
    public class SerilogLogger : Logging.ILogger
    {
        /// <summary>Represents the context property name of the serilog logger.
        /// </summary>
        public string ContextPropertyName { get; }
        /// <summary>Represents the serilog logger.
        /// </summary>
        public SerilogILogger SerilogILogger { get; }

        /// <summary>Default constructor.
        /// </summary>
        /// <param name="contextPropertyName"></param>
        /// <param name="logger"></param>
        public SerilogLogger(string contextPropertyName, SerilogILogger logger)
        {
            ContextPropertyName = contextPropertyName;
            SerilogILogger = logger;
        }

        /// <summary>Represents is debug enable of the logger.
        /// </summary>
        public bool IsDebugEnabled => SerilogILogger.IsEnabled(LogLevel.Debug);

        /// <summary>Log debug message.
        /// </summary>
        /// <param name="message"></param>
        public void Debug(string message)
        {
            SerilogILogger.Debug(message);
        }
        /// <summary>Log debug message with exception.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="exception"></param>
        public void Debug(string message, Exception exception)
        {
            SerilogILogger.Debug(exception, message);
        }
        /// <summary>Log debug message with format and arguments.
        /// </summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        public void DebugFormat(string format, params object[] args)
        {
            SerilogILogger.Debug(format, args);
        }
        /// <summary>Log error message.
        /// </summary>
        /// <param name="message"></param>
        public void Error(string message)
        {
            SerilogILogger.Error(message);
        }
        /// <summary>Log error message with exception.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="exception"></param>
        public void Error(string message, Exception exception)
        {
            SerilogILogger.Error(exception, message);
        }
        /// <summary>Log error message with format and arguments.
        /// </summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        public void ErrorFormat(string format, params object[] args)
        {
            SerilogILogger.Error(format, args);
        }
        /// <summary>Log fatal message.
        /// </summary>
        /// <param name="message"></param>
        public void Fatal(string message)
        {
            SerilogILogger.Fatal(message);
        }
        /// <summary>Log fatal message with exception.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="exception"></param>
        public void Fatal(string message, Exception exception)
        {
            SerilogILogger.Fatal(message, exception);
        }
        /// <summary>Log fatal message with format and arguments.
        /// </summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        public void FatalFormat(string format, params object[] args)
        {
            SerilogILogger.Fatal(format, args);
        }
        /// <summary>Log info message.
        /// </summary>
        /// <param name="message"></param>
        public void Info(string message)
        {
            SerilogILogger.Information(message);
        }
        /// <summary>Log info message with exception.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="exception"></param>
        public void Info(string message, Exception exception)
        {
            SerilogILogger.Information(exception, message);
        }
        /// <summary>Log info message with format and arguments.
        /// </summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        public void InfoFormat(string format, params object[] args)
        {
            SerilogILogger.Information(format, args);
        }
        /// <summary>Log warning message.
        /// </summary>
        /// <param name="message"></param>
        public void Warn(string message)
        {
            SerilogILogger.Warning(message);
        }
        /// <summary>Log warning message with exception.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="exception"></param>
        public void Warn(string message, Exception exception)
        {
            SerilogILogger.Warning(message, exception);
        }
        /// <summary>Log warning message with format and arguments.
        /// </summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        public void WarnFormat(string format, params object[] args)
        {
            SerilogILogger.Warning(format, args);
        }
    }
}
