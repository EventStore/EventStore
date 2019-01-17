using System.Collections.Generic;

namespace EventStore.Plugins
{
    public interface IEventStoreServiceFactory
    {
        IList<IEventStoreService> Create();
    }
}
