using System.Collections.Generic;

namespace EventStore.Plugins
{
    public interface IEventStoreControllerFactory
    {
        IList<IEventStoreController> Create();
    }
}
