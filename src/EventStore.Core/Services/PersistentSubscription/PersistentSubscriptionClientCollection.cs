using System;
using System.Collections.Generic;
using System.Linq;
using EventStore.Core.Data;
using EventStore.Core.Services.PersistentSubscription.ConsumerStrategy;

namespace EventStore.Core.Services.PersistentSubscription {
	internal class PersistentSubscriptionClientCollection {
		private readonly IPersistentSubscriptionConsumerStrategy _consumerStrategy;

		private readonly Dictionary<Guid, PersistentSubscriptionClient> _hash =
			new Dictionary<Guid, PersistentSubscriptionClient>();

		public int Count {
			get { return _hash.Count; }
		}

		public PersistentSubscriptionClientCollection(IPersistentSubscriptionConsumerStrategy consumerStrategy) {
			_consumerStrategy = consumerStrategy;
		}

		public void AddClient(PersistentSubscriptionClient client) {
			_hash.Add(client.CorrelationId, client);
			_consumerStrategy.ClientAdded(client);
		}

		public ConsumerPushResult PushMessageToClient(ResolvedEvent ev, int retryCount) {
			return _consumerStrategy.PushMessageToClient(ev, retryCount);
		}

		public IEnumerable<ResolvedEvent> RemoveClientByConnectionId(Guid connectionId) {
			var clients = _hash.Values.Where(x => x.ConnectionId == connectionId).ToList();
			return clients.SelectMany(client => RemoveClientByCorrelationId(client.CorrelationId, false));
		}

		public void ShutdownAll() {
			foreach (var client in _hash.Values.ToArray()) {
				RemoveClientByCorrelationId(client.CorrelationId, true);
			}
		}

		public IEnumerable<ResolvedEvent> RemoveClientByCorrelationId(Guid correlationId, bool sendDropNotification) {
			PersistentSubscriptionClient client;
			if (!_hash.TryGetValue(correlationId, out client)) return new ResolvedEvent[0];
			_hash.Remove(client.CorrelationId);
			_consumerStrategy.ClientRemoved(client);
			if (sendDropNotification) {
				client.SendDropNotification();
			}

			return client.GetUnconfirmedEvents();
		}

		public IEnumerable<PersistentSubscriptionClient> GetAll() {
			return _hash.Values;
		}

		public void RemoveProcessingMessages(Guid correlationId, Guid[] processedEventIds) {
			PersistentSubscriptionClient client;
			if (!_hash.TryGetValue(correlationId, out client)) return;
			client.RemoveFromProcessing(processedEventIds);
		}

		public void RemoveProcessingMessage(Guid eventId) {
			foreach (var client in _hash.Values) {
				if (client.RemoveFromProcessing(new[] {eventId})) return;
			}
		}
	}
}
