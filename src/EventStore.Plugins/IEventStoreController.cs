using System;
using System.Collections.Generic;

namespace EventStore.Plugins
{
    public interface IEventStoreController
    {
        IDictionary<string, Action> RegisteredActions { get; }
    }
}
