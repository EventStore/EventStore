using System.Collections.Generic;
using System.Threading.Tasks;

namespace EventStore.Core.Services.Plugins
{
    public interface IPluginPublisher
    {
        Task<bool> TryPublish(IDictionary<string, dynamic> message);
    }
}