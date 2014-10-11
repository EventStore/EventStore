using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace EventStore.Rags
{
    public static class IEnumerableExtensions
    {
        public static T ApplyTo<T>(this IEnumerable<OptionSource> source) where T:class,new()
        {
            return OptionApplicator.Get<T>(source);
        }

        public static IEnumerable<T> Flatten<T>(this IEnumerable<IEnumerable<T>> source)
        {
            return source.SelectMany(x => x);
        }
    }
}