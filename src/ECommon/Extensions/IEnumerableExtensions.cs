using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace ECommon.Extensions
{
    public static class IEnumerableExtensions
    {
        public static void ForEach<T>(this IEnumerable<T> enumerable, Action<T> action)
        {
            foreach (var element in enumerable)
            {
                action(element);
            }
        }
        public static IEnumerable<T> Safe<T>(this IEnumerable<T> collection)
        {
            return collection ?? Enumerable.Empty<T>();
        }

        public static bool Contains<T>(this IEnumerable<T> collection, Predicate<T> condition)
        {
            return collection.Any(x => condition(x));
        }

        public static bool IsEmpty<T>(this IEnumerable<T> collection)
        {
            if (collection == null)
                return true;
            var coll = collection as ICollection;
            if (coll != null)
                return coll.Count == 0;
            return !collection.Any();
        }

        public static bool IsNotEmpty<T>(this IEnumerable<T> collection)
        {
            return !IsEmpty(collection);
        }
    }
}
