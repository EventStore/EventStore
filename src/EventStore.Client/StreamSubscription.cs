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

		internal static async Task<StreamSubscription> Confirm(
			IAsyncEnumerable<(SubscriptionConfirmation, ResolvedEvent)> read,
			Func<StreamSubscription, ResolvedEvent, CancellationToken, Task> eventAppeared,
			Action<StreamSubscription, SubscriptionDroppedReason, Exception> subscriptionDropped = default,
			CancellationToken cancellationToken = default) {
			var enumerator = read.GetAsyncEnumerator(cancellationToken);
			if (!await enumerator.MoveNextAsync(cancellationToken).ConfigureAwait(false) ||
			    enumerator.Current.Item1 == SubscriptionConfirmation.None) {
				throw new InvalidOperationException();
			}

			return new StreamSubscription(enumerator, eventAppeared, subscriptionDropped);
		}

		private StreamSubscription(
			IAsyncEnumerator<(SubscriptionConfirmation, ResolvedEvent)> events,
			Func<StreamSubscription, ResolvedEvent, CancellationToken, Task> eventAppeared,
			Action<StreamSubscription, SubscriptionDroppedReason, Exception> subscriptionDropped = default) {
			if (events == null) {
				throw new ArgumentNullException(nameof(events));
			}

			if (eventAppeared == null) {
				throw new ArgumentNullException(nameof(eventAppeared));
			}

			_disposed = new CancellationTokenSource();
			_events = new Enumerable(events);
			_eventAppeared = eventAppeared;
			_subscriptionDropped = subscriptionDropped;
			_subscriptionDroppedInvoked = 0;

			Task.Run(Subscribe);
		}

		private async Task Subscribe() {
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

		private class Enumerable : IAsyncEnumerable<ResolvedEvent> {
			private readonly IAsyncEnumerator<(SubscriptionConfirmation, ResolvedEvent resolvedEvent)> _inner;

			public Enumerable(IAsyncEnumerator<(SubscriptionConfirmation, ResolvedEvent resolvedEvent)> inner) {
				if (inner == null) throw new ArgumentNullException(nameof(inner));
				_inner = inner;
			}

			public IAsyncEnumerator<ResolvedEvent> GetAsyncEnumerator(CancellationToken cancellationToken = default)
				=> new Enumerator(_inner);

			private class Enumerator : IAsyncEnumerator<ResolvedEvent> {
				private readonly IAsyncEnumerator<(SubscriptionConfirmation, ResolvedEvent resolvedEvent)> _inner;

				public Enumerator(IAsyncEnumerator<(SubscriptionConfirmation, ResolvedEvent resolvedEvent)> inner) {
					if (inner == null) throw new ArgumentNullException(nameof(inner));
					_inner = inner;
				}

				public ValueTask DisposeAsync() => _inner.DisposeAsync();

				public ValueTask<bool> MoveNextAsync() => _inner.MoveNextAsync();

				public ResolvedEvent Current => _inner.Current.resolvedEvent;
			}
		}
	}
}
