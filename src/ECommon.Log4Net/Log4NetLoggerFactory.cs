using System;
using System.IO;
using ECommon.Logging;
using log4net;
using log4net.Appender;
using log4net.Config;
using log4net.Layout;

namespace ECommon.Log4Net
{
    /// <summary>Log4Net based logger factory.
    /// </summary>
    public class Log4NetLoggerFactory : ILoggerFactory
    {
        private readonly string loggerRepository;

        /// <summary>Parameterized constructor.
        /// </summary>
        /// <param name="configFile"></param>
        public Log4NetLoggerFactory(string configFile, string loggerRepository = "NetStandardRepository")
        {
            var file = new FileInfo(configFile);
            if (!file.Exists)
            {
                file = new FileInfo(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, configFile));
            }
            var repository = LogManager.GetRepository(loggerRepository);
            if (repository != null)
            {
                return;
            }

            repository = LogManager.CreateRepository(loggerRepository);
            if (file.Exists)
            {
                XmlConfigurator.ConfigureAndWatch(repository, file);
            }
            else
            {
                BasicConfigurator.Configure(repository, new ConsoleAppender { Layout = new PatternLayout() });
            }
            this.loggerRepository = loggerRepository;
        }
        /// <summary>Create a new Log4NetLogger instance.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public ILogger Create(string name)
        {
            return new Log4NetLogger(LogManager.GetLogger(loggerRepository, name));
        }
        /// <summary>Create a new Log4NetLogger instance.
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public ILogger Create(Type type)
        {
            return new Log4NetLogger(LogManager.GetLogger(loggerRepository, type));
        }
    }
}
