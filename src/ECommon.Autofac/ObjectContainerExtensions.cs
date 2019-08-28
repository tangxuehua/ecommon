using Autofac;
using ECommon.Components;
using System;

namespace ECommon.Autofac
{
    public static class ObjectContainerExtensions
    {
        public static void SetContainer(this IObjectContainer objectContainer, ILifetimeScope container)
        {
            if (!(objectContainer is AutofacObjectContainer autofacObjectContainer))
                throw new InvalidOperationException($"instance of {nameof(ObjectContainer)} is not of type {nameof(AutofacObjectContainer)}");
            autofacObjectContainer.SetContainer(container);
        }
    }
}