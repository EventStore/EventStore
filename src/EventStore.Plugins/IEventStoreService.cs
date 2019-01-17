using System.Collections.Generic;
using System.Threading.Tasks;

namespace EventStore.Plugins
{
    public interface IEventStoreService
    {
        string Name { get; }
        Task<bool> Start();
        Task<bool> Stop();
        bool AutoStart { get; }
        Task<bool> TryHandle(IDictionary<string, dynamic> request);
        IDictionary<string, dynamic> GetStats();
    }
}
