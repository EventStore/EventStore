using System.Collections.Generic;

namespace EventStore.Core.Services.Plugins
{
    public interface IPluginPublisher
    {
        bool TryPublish(IDictionary<string, dynamic> message);
    }
}
