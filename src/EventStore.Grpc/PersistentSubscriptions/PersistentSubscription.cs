using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;

namespace EventStore.Grpc.PersistentSubscriptions {
	public class PersistentSubscription : IDisposable {
		private readonly PersistentSubscriptions.PersistentSubscriptionsClient _client;
		private readonly UserCredentials _userCredentials;
		private readonly ReadReq.Types.Options _options;
		private readonly bool _autoAck;
		private readonly Func<PersistentSubscription, ResolvedEvent, int?, CancellationToken, Task> _eventAppeared;
		private readonly Action<PersistentSubscription, SubscriptionDroppedReason, Exception> _subscriptionDropped;
		private readonly CancellationTokenSource _disposed;
		private readonly TaskCompletionSource<bool> _started;
		private int _subscriptionDroppedInvoked;
		private AsyncDuplexStreamingCall<ReadReq, ReadResp> _call;

		public Task Started => _started.Task;

		internal PersistentSubscription(
			PersistentSubscriptions.PersistentSubscriptionsClient client,
			ReadReq.Types.Options options,
			bool autoAck,
			Func<PersistentSubscription, ResolvedEvent, int?, CancellationToken, Task> eventAppeared,
			Action<PersistentSubscription, SubscriptionDroppedReason, Exception> subscriptionDropped,
			UserCredentials userCredentials) {
			_client = client;
			_userCredentials = userCredentials;
			_options = options;
			_autoAck = autoAck;
			_eventAppeared = eventAppeared;
			_subscriptionDropped = subscriptionDropped;
			_disposed = new CancellationTokenSource();
			_started = new TaskCompletionSource<bool>();
			Task.Run(Start);
		}

		private async Task Start() {
			_call = _client.Read(RequestMetadata.Create(_userCredentials), cancellationToken: _disposed.Token);

			try {
				await _call.RequestStream.WriteAsync(new ReadReq {
					Options = _options
				});

				if (!await _call.ResponseStream.MoveNext(_disposed.Token)
				    || _call.ResponseStream.Current.ContentCase != ReadResp.ContentOneofCase.Empty) {
					throw new InvalidOperationException();
				}
			} catch (Exception ex) {
				SubscriptionDropped(SubscriptionDroppedReason.ServerError, ex);
				return;
			} finally {
				_started.SetResult(true);
			}

#pragma warning disable 4014
			Task.Run(Subscribe);
#pragma warning restore 4014
		}

		public Task Ack(params Guid[] eventIds) {
			if (eventIds.Length > 2000) {
				throw new ArgumentException();
			}

			return AckInternal(eventIds);
		}

		public Task Ack(IEnumerable<Guid> eventIds) => Ack(eventIds.ToArray());

		public Task Ack(params ResolvedEvent[] resolvedEvents) =>
			Ack(Array.ConvertAll(resolvedEvents, resolvedEvent => resolvedEvent.OriginalEvent.EventId));

		public Task Ack(IEnumerable<ResolvedEvent> resolvedEvents) =>
			Ack(resolvedEvents.Select(resolvedEvent => resolvedEvent.OriginalEvent.EventId));

		public Task Nack(PersistentSubscriptionNakEventAction action, string reason, params Guid[] eventIds) {
			if (eventIds.Length > 2000) {
				throw new ArgumentException();
			}

			return NackInternal(eventIds, action, reason);
		}

		public Task Nack(PersistentSubscriptionNakEventAction action, string reason,
			params ResolvedEvent[] resolvedEvents) =>
			Nack(action, reason,
				Array.ConvertAll(resolvedEvents, resolvedEvent => resolvedEvent.OriginalEvent.EventId));

		public void Dispose() {
			if (_disposed.IsCancellationRequested) {
				return;
			}

			SubscriptionDropped(SubscriptionDroppedReason.Disposed);

			_disposed.Dispose();
		}

		private async Task Subscribe() {
			try {
				while (await _call.ResponseStream.MoveNext() && !_disposed.IsCancellationRequested) {
					var current = _call.ResponseStream.Current;
					switch (current.ContentCase) {
						case ReadResp.ContentOneofCase.Event:
							try {
								await _eventAppeared(this, ConvertToResolvedEvent(current),
									current.Event.CountCase switch {
										ReadResp.Types.ReadEvent.CountOneofCase.RetryCount => current.Event.RetryCount,
										_ => default
									}, _disposed.Token);
								if (_autoAck) {
									await AckInternal(
										(current.Event.Link?.Id?.ValueCase ?? current.Event.Event.Id.ValueCase) switch {
											UUID.ValueOneofCase.String => Guid.Parse(
												(current.Event.Link?.Id ?? current.Event.Event.Id).String),
											_ => throw new NotSupportedException()
										});
								}
							} catch (Exception ex) when (ex is ObjectDisposedException ||
							                             ex is OperationCanceledException) {
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

							break;
					}
				}
			} catch (Exception ex) {
				try {
					SubscriptionDropped(SubscriptionDroppedReason.ServerError, ex);
				} finally {
					_disposed.Cancel();
				}
			}

			ResolvedEvent ConvertToResolvedEvent(ReadResp response) =>
				new ResolvedEvent(
					ConvertToEventRecord(response.Event.Event),
					ConvertToEventRecord(response.Event.Link),
					response.Event.PositionCase switch {
						ReadResp.Types.ReadEvent.PositionOneofCase.CommitPosition => new Position(
							response.Event.CommitPosition, 0).ToInt64().commitPosition,
						ReadResp.Types.ReadEvent.PositionOneofCase.NoPosition => default(long?),
						_ => throw new InvalidOperationException()
					});

			EventRecord ConvertToEventRecord(ReadResp.Types.ReadEvent.Types.RecordedEvent e) =>
				e == null
					? null
					: new EventRecord(
						e.StreamName,
						e.Id.ValueCase switch {
							UUID.ValueOneofCase.String => Guid.Parse(e.Id.String),
							_ => throw new NotSupportedException()
						},
						new StreamRevision(e.StreamRevision),
						new Position(e.CommitPosition, e.PreparePosition),
						e.Metadata,
						e.Data.ToByteArray(),
						e.CustomMetadata.ToByteArray());
		}

		private void SubscriptionDropped(SubscriptionDroppedReason reason, Exception ex = null) {
			if (Interlocked.CompareExchange(ref _subscriptionDroppedInvoked, 1, 0) == 1) {
				return;
			}

			_call?.Dispose();
			_subscriptionDropped?.Invoke(this, reason, ex);
			_disposed.Dispose();
		}

		private Task AckInternal(params Guid[] ids) =>
			_call.RequestStream.WriteAsync(new ReadReq {
				Ack = new ReadReq.Types.Ack {
					Ids = {
						Array.ConvertAll(ids, id => new UUID {
							String = id.ToString("n")
						})
					}
				}
			});

		private Task NackInternal(Guid[] ids, PersistentSubscriptionNakEventAction action, string reason) =>
			_call.RequestStream.WriteAsync(new ReadReq {
				Nack = new ReadReq.Types.Nack {
					Ids = {
						Array.ConvertAll(ids, id => new UUID {
							String = id.ToString("n")
						})
					},
					Action = action switch {
						PersistentSubscriptionNakEventAction.Park => ReadReq.Types.Nack.Types.Action.Park,
						PersistentSubscriptionNakEventAction.Retry => ReadReq.Types.Nack.Types.Action.Retry,
						PersistentSubscriptionNakEventAction.Skip => ReadReq.Types.Nack.Types.Action.Skip,
						PersistentSubscriptionNakEventAction.Stop => ReadReq.Types.Nack.Types.Action.Stop,
						PersistentSubscriptionNakEventAction.Unknown => ReadReq.Types.Nack.Types.Action.Unknown,
						_ => throw new ArgumentOutOfRangeException(nameof(action))
					},
					Reason = reason
				}
			});
	}
}
