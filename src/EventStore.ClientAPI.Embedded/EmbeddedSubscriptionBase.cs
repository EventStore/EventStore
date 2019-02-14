using System;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using EventStore.ClientAPI.Common.Utils;
using EventStore.ClientAPI.Exceptions;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.UserManagement;

namespace EventStore.ClientAPI.Embedded {
	internal abstract class EmbeddedSubscriptionBase<TSubscription> : IEmbeddedSubscription
		where TSubscription : EventStoreSubscription {
		private readonly ILogger _log;
		protected readonly Guid ConnectionId;
		private readonly TaskCompletionSource<TSubscription> _source;
		private readonly Action<EventStoreSubscription, SubscriptionDropReason, Exception> _subscriptionDropped;
		private int _actionExecuting;
		private readonly ConcurrentQueue<Action> _actionQueue;
		private int _unsubscribed;
		protected TSubscription Subscription;
		protected IPublisher Publisher;
		protected string StreamId;
		protected Guid CorrelationId;

		protected EmbeddedSubscriptionBase(
			ILogger log, IPublisher publisher, Guid connectionId, TaskCompletionSource<TSubscription> source,
			string streamId,
			Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped) {
			Ensure.NotNull(source, "source");
			Ensure.NotNull(streamId, "streamId");
			Ensure.NotNull(publisher, "publisher");

			Publisher = publisher;
			StreamId = streamId;
			ConnectionId = connectionId;
			_log = log;
			_source = source;
			_subscriptionDropped = subscriptionDropped ?? ((a, b, c) => { });
			_actionQueue = new ConcurrentQueue<Action>();
		}

		public void DropSubscription(Core.Services.SubscriptionDropReason reason, Exception ex) {
			switch (reason) {
				case Core.Services.SubscriptionDropReason.AccessDenied:
					DropSubscription(SubscriptionDropReason.AccessDenied,
						ex ?? new AccessDeniedException(string.Format(
							"Subscription to '{0}' failed due to access denied.",
							StreamId == string.Empty ? "<all>" : StreamId)));
					break;
				case Core.Services.SubscriptionDropReason.Unsubscribed:
					Unsubscribe();
					break;
				case Core.Services.SubscriptionDropReason.NotFound:
					DropSubscription(SubscriptionDropReason.NotFound,
						new ArgumentException("Subscription not found"));
					break;
			}
		}

		public void ConfirmSubscription(long lastCommitPosition, long? lastEventNumber) {
			if (lastCommitPosition < -1)
				throw new ArgumentOutOfRangeException("lastCommitPosition",
					string.Format("Invalid lastCommitPosition {0} on subscription confirmation.", lastCommitPosition));
			if (Subscription != null)
				throw new Exception("Double confirmation of subscription.");

			Subscription = CreateVolatileSubscription(lastCommitPosition, lastEventNumber);
			_source.SetResult(Subscription);
		}

		protected abstract TSubscription CreateVolatileSubscription(long lastCommitPosition, long? lastEventNumber);

		public void Unsubscribe() {
			DropSubscription(SubscriptionDropReason.UserInitiated, null);
		}

		private void DropSubscription(SubscriptionDropReason reason, Exception exception) {
			if (Interlocked.CompareExchange(ref _unsubscribed, 1, 0) == 0) {
				if (reason != SubscriptionDropReason.UserInitiated) {
					if (exception == null)
						throw new Exception(string.Format("No exception provided for subscription drop reason '{0}",
							reason));
					_source.TrySetException(exception);
				}

				if (reason == SubscriptionDropReason.UserInitiated && Subscription != null)
					Publisher.Publish(new ClientMessage.UnsubscribeFromStream(Guid.NewGuid(), CorrelationId,
						new NoopEnvelope(), SystemAccount.Principal));

				if (Subscription != null)
					ExecuteActionAsync(() => _subscriptionDropped(Subscription, reason, exception));
			}
		}

		private void ExecuteActionAsync(Action action) {
			_actionQueue.Enqueue(action);
			if (Interlocked.CompareExchange(ref _actionExecuting, 1, 0) == 0)
				ThreadPool.QueueUserWorkItem(ExecuteActions);
		}

		private void ExecuteActions(object state) {
			do {
				Action action;
				while (_actionQueue.TryDequeue(out action)) {
					try {
						action();
					} catch (Exception exc) {
						_log.Error(exc, "Exception during executing user callback: {e}.", exc.Message);
					}
				}

				Interlocked.Exchange(ref _actionExecuting, 0);
			} while (!_actionQueue.IsEmpty && Interlocked.CompareExchange(ref _actionExecuting, 1, 0) == 0);
		}

		public abstract void Start(Guid correlationId);
	}
}
