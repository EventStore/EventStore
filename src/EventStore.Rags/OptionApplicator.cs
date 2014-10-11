using System.Collections.Generic;

namespace EventStore.Rags
{
    public class OptionApplicator
    {
        public static T Get<T>(IEnumerable<OptionSource> source) where T:class,new()
        {
            return new T();
        }
    }
}