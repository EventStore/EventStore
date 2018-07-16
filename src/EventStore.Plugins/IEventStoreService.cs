using System.Collections.Generic;

namespace EventStore.Plugins
{
    public interface IEventStoreService
    {
        string Name { get; }
        void Start();
        void Stop();
        bool AutoStart { get; }
        bool Try(IDictionary<string, dynamic> request);
        IDictionary<string, dynamic> GetStats();
    }
}
