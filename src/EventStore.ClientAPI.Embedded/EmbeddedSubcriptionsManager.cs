using System;
using System.Collections.Generic;

namespace EventStore.ClientAPI.Embedded
{
    internal class EmbeddedSubcriptionsManager
    {
        private readonly IDictionary<Guid, EmbeddedSubscription> _activeSubscriptions;
        
        public EmbeddedSubcriptionsManager()
        {
            _activeSubscriptions = new Dictionary<Guid, EmbeddedSubscription>();
        }

        public bool TryGetActiveSubscription(Guid correlationId, out EmbeddedSubscription subscription)
        {
            return _activeSubscriptions.TryGetValue(correlationId, out subscription);
        }

        public void StartSubscription(Guid correlationId, EmbeddedSubscription subscription)
        {
            _activeSubscriptions.Add(correlationId, subscription);
            subscription.Start(correlationId);
        }
    }
}