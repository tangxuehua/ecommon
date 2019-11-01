using System;

namespace ECommon.Logging
{
    /// <summary>Represents a logger interface.
    /// </summary>
    public interface ILogger
    {
        /// <summary>Represents whether the debug log level is enabled.
        /// </summary>
        bool IsDebugEnabled { get; }
        /// <summary>Write a debug level log message.
        /// </summary>
        /// <param name="message"></param>
        void Debug(string message);
        /// <summary>Write a debug level log message.
        /// </summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        void DebugFormat(string format, params object[] args);
        /// <summary>Write a debug level log message.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="exception"></param>
        void Debug(string message, Exception exception);

        /// <summary>Write a info level log message.
        /// </summary>
        /// <param name="message"></param>
        void Info(string message);
        /// <summary>Write a info level log message.
        /// </summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        void InfoFormat(string format, params object[] args);
        /// <summary>Write a info level log message.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="exception"></param>
        void Info(string message, Exception exception);

        /// <summary>Write an error level log message.
        /// </summary>
        /// <param name="message"></param>
        void Error(string message);
        /// <summary>Write an error level log message.
        /// </summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        void ErrorFormat(string format, params object[] args);
        /// <summary>Write an error level log message.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="exception"></param>
        void Error(string message, Exception exception);

        /// <summary>Write a warnning level log message.
        /// </summary>
        /// <param name="message"></param>
        void Warn(string message);
        /// <summary>Write a warnning level log message.
        /// </summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        void WarnFormat(string format, params object[] args);
        /// <summary>Write a warnning level log message.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="exception"></param>
        void Warn(string message, Exception exception);

        /// <summary>Write a fatal level log message.
        /// </summary>
        /// <param name="message"></param>
        void Fatal(string message);
        /// <summary>Write a fatal level log message.
        /// </summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        void FatalFormat(string format, params object[] args);
        /// <summary>Write a fatal level log message.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="exception"></param>
        void Fatal(string message, Exception exception);
    }
}