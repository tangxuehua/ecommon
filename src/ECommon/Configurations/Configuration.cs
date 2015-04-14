using System;
using ECommon.Components;
using ECommon.IO;
using ECommon.Logging;
using ECommon.Scheduling;
using ECommon.Serializing;

namespace ECommon.Configurations
{
    public class Configuration
    {
        /// <summary>Get the configuration setting information.
        /// </summary>
        public Setting Setting { get; private set; }
        /// <summary>Provides the singleton access instance.
        /// </summary>
        public static Configuration Instance { get; private set; }

        private Configuration(Setting setting)
        {
            Setting = setting ?? new Setting();
        }

        public static Configuration Create(Setting setting = null)
        {
            if (Instance != null)
            {
                throw new Exception("Could not create configuration instance twice.");
            }
            Instance = new Configuration(setting);
            return Instance;
        }

        public Configuration SetDefault<TService, TImplementer>(LifeStyle life = LifeStyle.Singleton)
            where TService : class
            where TImplementer : class, TService
        {
            ObjectContainer.Register<TService, TImplementer>(life);
            return this;
        }
        public Configuration SetDefault<TService, TImplementer>(TImplementer instance)
            where TService : class
            where TImplementer : class, TService
        {
            ObjectContainer.RegisterInstance<TService, TImplementer>(instance);
            return this;
        }

        public Configuration RegisterCommonComponents()
        {
            SetDefault<ILoggerFactory, EmptyLoggerFactory>();
            SetDefault<IBinarySerializer, DefaultBinarySerializer>();
            SetDefault<IJsonSerializer, NotImplementedJsonSerializer>();
            SetDefault<IScheduleService, ScheduleService>();
            SetDefault<IOHelper, IOHelper>();
            return this;
        }
        public Configuration RegisterUnhandledExceptionHandler()
        {
            var logger = ObjectContainer.Resolve<ILoggerFactory>().Create(GetType().FullName);
            AppDomain.CurrentDomain.UnhandledException += (sender, e) => logger.ErrorFormat("Unhandled exception: {0}", e.ExceptionObject);
            return this;
        }
    }
}
