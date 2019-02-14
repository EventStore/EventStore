using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace EventStore.ClientAPI.Embedded {
	internal class EmbeddedSubcriptionsManager {
		private readonly IDictionary<Guid, IEmbeddedSubscription> _activeSubscriptions;

		public EmbeddedSubcriptionsManager() {
			_activeSubscriptions = new ConcurrentDictionary<Guid, IEmbeddedSubscription>();
		}

		public bool TryGetActiveSubscription(Guid correlationId, out IEmbeddedSubscription subscription) {
			return _activeSubscriptions.TryGetValue(correlationId, out subscription);
		}

		public void StartSubscription(Guid correlationId, IEmbeddedSubscription subscription) {
			_activeSubscriptions.Add(correlationId, subscription);
			subscription.Start(correlationId);
		}
	}
}
