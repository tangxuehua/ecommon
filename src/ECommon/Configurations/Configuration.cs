using System;
using ECommon.Components;
using ECommon.IO;
using ECommon.Logging;
using ECommon.Scheduling;
using ECommon.Serializing;
using ECommon.Socketing.Framing;
using ECommon.Utilities;

namespace ECommon.Configurations
{
    public class Configuration
    {
        /// <summary>Provides the singleton access instance.
        /// </summary>
        public static Configuration Instance { get; private set; }

        private Configuration() { }

        public static Configuration Create()
        {
            Instance = new Configuration();
            return Instance;
        }

        public Configuration SetDefault<TService, TImplementer>(string serviceName = null, LifeStyle life = LifeStyle.Singleton)
            where TService : class
            where TImplementer : class, TService
        {
            ObjectContainer.Register<TService, TImplementer>(serviceName, life);
            return this;
        }
        public Configuration SetDefault<TService, TImplementer>(TImplementer instance, string serviceName = null)
            where TService : class
            where TImplementer : class, TService
        {
            ObjectContainer.RegisterInstance<TService, TImplementer>(instance, serviceName);
            return this;
        }

        public Configuration RegisterCommonComponents()
        {
            SetDefault<ILoggerFactory, EmptyLoggerFactory>();
            SetDefault<IBinarySerializer, DefaultBinarySerializer>();
            SetDefault<IJsonSerializer, NotImplementedJsonSerializer>();
            SetDefault<IScheduleService, ScheduleService>(null, LifeStyle.Transient);
            SetDefault<IMessageFramer, LengthPrefixMessageFramer>(null, LifeStyle.Transient);
            SetDefault<IOHelper, IOHelper>();
            SetDefault<IPerformanceService, DefaultPerformanceService>(null, LifeStyle.Transient);
            return this;
        }
        public Configuration RegisterUnhandledExceptionHandler()
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                var logger = ObjectContainer.Resolve<ILoggerFactory>().Create(GetType().FullName);
                logger.ErrorFormat("Unhandled exception: {0}", e.ExceptionObject);
            };
            return this;
        }
        public Configuration BuildContainer()
        {
            ObjectContainer.Build();
            return this;
        }
    }
}
