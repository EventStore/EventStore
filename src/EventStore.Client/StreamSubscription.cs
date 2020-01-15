using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace EventStore.Client {
	public class StreamSubscription : IDisposable {
		private readonly IAsyncEnumerable<ResolvedEvent> _events;
		private readonly Func<StreamSubscription, ResolvedEvent, CancellationToken, Task> _eventAppeared;
		private readonly Action<StreamSubscription, SubscriptionDroppedReason, Exception> _subscriptionDropped;
		private readonly CancellationTokenSource _disposed;
		private int _subscriptionDroppedInvoked;

		public StreamSubscription(
			IAsyncEnumerable<ResolvedEvent> events,
			Func<StreamSubscription, ResolvedEvent, CancellationToken, Task> eventAppeared,
			Action<StreamSubscription, SubscriptionDroppedReason, Exception> subscriptionDropped = default) {
			if (events == null) {
				throw new ArgumentNullException(nameof(events));
			}

			if (eventAppeared == null) {
				throw new ArgumentNullException(nameof(eventAppeared));
			}

			_disposed = new CancellationTokenSource();
			_events = events;
			_eventAppeared = eventAppeared;
			_subscriptionDropped = subscriptionDropped;
			_subscriptionDroppedInvoked = 0;

			Task.Run(Subscribe);
		}

		private async Task Subscribe() {
			subscribe:
			try {
				await foreach (var resolvedEvent in _events.ConfigureAwait(false)) {
					try {
						if (_disposed.IsCancellationRequested) {
							SubscriptionDropped(SubscriptionDroppedReason.Disposed);
							return;
						}

						await _eventAppeared(this, resolvedEvent, _disposed.Token).ConfigureAwait(false);
					} catch (Exception ex) when (ex is ObjectDisposedException || ex is OperationCanceledException) {
						SubscriptionDropped(SubscriptionDroppedReason.Disposed);
						return;
					} catch (Exception ex) {
						try {
							SubscriptionDropped(SubscriptionDroppedReason.SubscriberError, ex);
						} finally {
							_disposed.Cancel();
						}

						return;
					}
				}
			} catch (StreamNotFoundException) {
				await Task.Delay(100).ConfigureAwait(false);
				goto subscribe;
			} catch (Exception ex) {
				try {
					SubscriptionDropped(SubscriptionDroppedReason.ServerError, ex);
				} finally {
					_disposed.Cancel();
				}
			}
		}

		public void Dispose() {
			if (_disposed.IsCancellationRequested) {
				return;
			}

			SubscriptionDropped(SubscriptionDroppedReason.Disposed);

			_disposed.Dispose();
		}

		private void SubscriptionDropped(SubscriptionDroppedReason reason, Exception ex = null) {
			if (Interlocked.CompareExchange(ref _subscriptionDroppedInvoked, 1, 0) == 1) {
				return;
			}
			
			_subscriptionDropped?.Invoke(this, reason, ex);
		}
	}
}
