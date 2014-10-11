using System.Collections.Generic;
using System.Text;

namespace EventStore.Rags
{
    public static class IEnumerableExtensions
    {
        public static string Dump(this IEnumerable<OptionSource> items)
        {
            var ret = new StringBuilder();
            foreach (var item in items)
            {
                ret.AppendFormat("{0}:{1} = '{2}'", item.Source, item.Name, item.Value);
            }
            return ret.ToString();
        }
    }
}