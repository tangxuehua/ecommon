using System;
using Autofac;
using ECommon.Components;

namespace ECommon.Autofac
{
    public class AutofacServiceProvider : IServiceProvider, IDisposable
    {
        private bool _disposed = false;

        /// <summary>
        /// Gets the underlying instance of <see cref="IContainer" />.
        /// </summary>
        public IContainer Container { get; }

        /// <summary>Default constructor.
        /// </summary>
        /// <param name="container"></param>
        /// <param name="autoSetObjectContainer"></param>
        public AutofacServiceProvider(IContainer container, bool autoSetObjectContainer = true)
        {
            Container = container;
            if (autoSetObjectContainer && ObjectContainer.Current is AutofacObjectContainer autofacObjectContainer)
            {
                autofacObjectContainer.Container = container;
            }
        }

        /// <summary>Resolve a service.
        /// </summary>
        /// <param name="serviceType">The service type.</param>
        /// <returns>The component instance that provides the service.</returns>
        public object GetService(Type serviceType)
        {
            return Container.Resolve(serviceType);
        }

        /// <summary>
        /// Releases the underlying container resources.
        /// </summary>
        /// <param name="disposing">
        /// <see langword="true" /> to release both managed and unmanaged resources;
        /// <see langword="false" /> to release only unmanaged resources.
        /// </param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _disposed = true;
                if (disposing)
                {
                    Container.Dispose();
                }
            }
        }

        /// <summary>
        /// Performs dispose operation.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
