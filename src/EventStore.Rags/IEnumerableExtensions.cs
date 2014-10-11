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

        public static IEnumerable<OptionSource> MergeOptions(this IEnumerable<IEnumerable<OptionSource>> sources, Func<OptionSource, OptionSource, bool> compare)
        {
            return MergeWithPreference(sources, x=> x.Name,compare);
        } 

        public static IEnumerable<T> MergeWithPreference<T>(this IEnumerable<IEnumerable<T>> sources, Func<T, string> keyFunc, Func<T, T, bool> compare)
        {
            var maps = sources.Select(s => s.ToDictionary(keyFunc)).ToList();
            var current = new Dictionary<string, T>();
            foreach (var kv in maps.SelectMany(m => m))
            {
                T val;
                if (!current.TryGetValue(kv.Key, out val) || compare(val, kv.Value))
                {
                    current[kv.Key] = kv.Value;
                }
            }
            return current.Values;
        }
    }
}