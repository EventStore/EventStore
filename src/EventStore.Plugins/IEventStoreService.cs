using System.Collections.Generic;

namespace EventStore.Plugins
{
    public interface IEventStoreService
    {
        string Name { get; }
        void Start();
        void Stop();
        bool AutoStart { get; }
        bool TryHandle(IDictionary<string, dynamic> request);
        IDictionary<string, dynamic> GetStats();
    }
}
