using System;
using System.Collections.Concurrent;
using EventStore.Core.Bus;
using EventStore.Projections.Core.Messages;

namespace EventStore.Projections.Core.Services {
	public sealed class ReaderSubscriptionDispatcher {
		private readonly ConcurrentDictionary<Guid, object> _map = new();
		private readonly IPublisher _publisher;

		public ReaderSubscriptionDispatcher(IPublisher publisher) {
			_publisher = publisher;
		}

		public Guid PublishSubscribe(
			ReaderSubscriptionManagement.Subscribe request, object subscriber) {
			var requestCorrelationId = request.SubscriptionId;
			_map.TryAdd(requestCorrelationId, subscriber);
			_publisher.Publish(request);
			return requestCorrelationId;
		}

		public void Cancel(Guid requestId) {
			_map.TryRemove(requestId, out _);
		}

		public IHandle<T> CreateSubscriber<T>() where T : EventReaderSubscriptionMessageBase {
			return new Subscriber<T>(this);
		}

		private void Handle<T>(T message) where T : EventReaderSubscriptionMessageBase {
			var correlationId = message.SubscriptionId;
			if (_map.TryGetValue(correlationId, out var subscriber)) {
				if (subscriber is IHandle<T> h)
					h.Handle(message);
			}
		}

		public void Subscribed(Guid correlationId, object subscriber) {
			_map.TryAdd(correlationId, subscriber);
		}

		private class Subscriber<T> : IHandle<T> where T : EventReaderSubscriptionMessageBase {
			private readonly ReaderSubscriptionDispatcher _host;

			public Subscriber(
				ReaderSubscriptionDispatcher host) {
				_host = host;
			}

			public void Handle(T message) {
				_host.Handle(message);
			}
		}
	}
}
