using System;
using SerilogILogger = Serilog.ILogger;
using LogLevel = Serilog.Events.LogEventLevel;

namespace ECommon.Serilog
{
    /// <summary>基于Serilog的ILogger实现
    /// </summary>
    public class SerilogLogger : Logging.ILogger
    {
        private readonly object _lockObj = new object();
        /// <summary>上下文属性名
        /// </summary>
        public string ContextPropertyName { get; }
        /// <summary>SerilogILogger
        /// </summary>
        public SerilogILogger SerilogILogger { get; }

        /// <summary>构造函数
        /// </summary>
        /// <param name="contextPropertyName"></param>
        /// <param name="logger"></param>
        public SerilogLogger(string contextPropertyName, SerilogILogger logger)
        {
            ContextPropertyName = contextPropertyName;
            SerilogILogger = logger;
        }

        /// <summary>是否支持Debug日志打印
        /// </summary>
        public bool IsDebugEnabled => SerilogILogger.IsEnabled(LogLevel.Debug);

        /// <summary>打印Debug级别的日志
        /// </summary>
        /// <param name="message"></param>
        public void Debug(string message)
        {
            SerilogILogger.Debug(message);
        }

        /// <summary>打印Debug级别的日志
        /// </summary>
        public void Debug(string message, Exception exception)
        {
            SerilogILogger.Debug(exception, message);
        }

        /// <summary>打印Debug级别的日志
        /// </summary>
        public void DebugFormat(string format, params object[] args)
        {
            SerilogILogger.Debug(format, args);
        }

        /// <summary>打印Error级别的日志
        /// </summary>
        public void Error(string message)
        {
            SerilogILogger.Error(message);
        }

        /// <summary>打印Error级别的日志
        /// </summary>
        public void Error(string message, Exception exception)
        {
            SerilogILogger.Error(exception, message);
        }

        /// <summary>打印Error级别的日志
        /// </summary>
        public void ErrorFormat(string format, params object[] args)
        {
            SerilogILogger.Error(format, args);
        }

        /// <summary>打印Fatal级别的日志
        /// </summary>
        public void Fatal(string message)
        {
            SerilogILogger.Fatal(message);
        }

        /// <summary>打印Fatal级别的日志
        /// </summary>
        public void Fatal(string message, Exception exception)
        {
            SerilogILogger.Fatal(message, exception);
        }

        /// <summary>打印Fatal级别的日志
        /// </summary>
        public void FatalFormat(string format, params object[] args)
        {
            SerilogILogger.Fatal(format, args);
        }

        /// <summary>打印Info级别的日志
        /// </summary>
        public void Info(string message)
        {
            SerilogILogger.Information(message);
        }

        /// <summary>打印Info级别的日志
        /// </summary>
        public void Info(string message, Exception exception)
        {
            SerilogILogger.Information(exception, message);
        }

        /// <summary>打印Info级别的日志
        /// </summary>
        public void InfoFormat(string format, params object[] args)
        {
            SerilogILogger.Information(format, args);
        }

        /// <summary>打印Warn级别的日志
        /// </summary>
        public void Warn(string message)
        {
            SerilogILogger.Warning(message);
        }

        /// <summary>打印Warn级别的日志
        /// </summary>
        public void Warn(string message, Exception exception)
        {
            SerilogILogger.Warning(message, exception);
        }

        /// <summary>打印Warn级别的日志
        /// </summary>
        public void WarnFormat(string format, params object[] args)
        {
            SerilogILogger.Warning(format, args);
        }
    }
}
