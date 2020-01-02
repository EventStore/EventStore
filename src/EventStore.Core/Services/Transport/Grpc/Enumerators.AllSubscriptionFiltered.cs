using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Client;
using EventStore.Common.Log;
using IEventFilter = EventStore.Core.Util.IEventFilter;
using IReadIndex = EventStore.Core.Services.Storage.ReaderIndex.IReadIndex;

namespace EventStore.Core.Services.Transport.Grpc {
	internal static partial class Enumerators {
		public class AllSubscriptionFiltered : ISubscriptionEnumerator {
			private static readonly ILogger Log = LogManager.GetLoggerFor<StreamSubscription>();

			private readonly Guid _subscriptionId;
			private readonly IPublisher _bus;
			private readonly bool _resolveLinks;
			private readonly IEventFilter _eventFilter;
			private readonly IPrincipal _user;
			private readonly IReadIndex _readIndex;
			private readonly TaskCompletionSource<bool> _subscriptionStarted;
			private readonly CancellationToken _cancellationToken;
			private IStreamSubscription _inner;

			public ResolvedEvent Current => _inner.Current;
			public Task Started => _subscriptionStarted.Task;
			public string SubscriptionId => _subscriptionId.ToString();

			public AllSubscriptionFiltered(IPublisher bus,
				Position? startPosition,
				bool resolveLinks,
				IEventFilter eventFilter,
				IPrincipal user,
				IReadIndex readIndex,
				CancellationToken cancellationToken) {
				if (bus == null) {
					throw new ArgumentNullException(nameof(bus));
				}

				if (eventFilter == null) {
					throw new ArgumentNullException(nameof(eventFilter));
				}

				if (readIndex == null) {
					throw new ArgumentNullException(nameof(readIndex));
				}

				_subscriptionId = Guid.NewGuid();
				_bus = bus;
				_resolveLinks = resolveLinks;
				_eventFilter = eventFilter;
				_user = user;
				_readIndex = readIndex;
				_cancellationToken = cancellationToken;
				_subscriptionStarted = new TaskCompletionSource<bool>();
				_subscriptionStarted.SetResult(true);

				_inner = startPosition == Position.End
					? (IStreamSubscription)new LiveStreamSubscription(_subscriptionId, _bus,
						Position.FromInt64(_readIndex.LastIndexedPosition, _readIndex.LastIndexedPosition),
						_resolveLinks, _eventFilter, _user, _cancellationToken)
					: new CatchupAllSubscription(_subscriptionId, bus, startPosition ?? Position.Start, resolveLinks,
						_eventFilter, user, readIndex, cancellationToken);
			}

			public async ValueTask<bool> MoveNextAsync() {
				ReadLoop:
				if (await _inner.MoveNextAsync().ConfigureAwait(false)) {
					return true;
				}

				if (_cancellationToken.IsCancellationRequested) {
					return false;
				}

				var currentPosition = _inner.CurrentPosition;
				await _inner.DisposeAsync().ConfigureAwait(false);
				Log.Trace("Subscription {subscriptionId} to $all:{eventFilter} reached the end, switching...",
					_subscriptionId, _eventFilter);

				if (_inner is LiveStreamSubscription)
					_inner = new CatchupAllSubscription(_subscriptionId, _bus, currentPosition, _resolveLinks,
						_eventFilter, _user, _readIndex, _cancellationToken);
				else
					_inner = new LiveStreamSubscription(_subscriptionId, _bus, currentPosition, _resolveLinks,
						_eventFilter, _user, _cancellationToken);

				goto ReadLoop;
			}

			public ValueTask DisposeAsync() => _inner.DisposeAsync();

			private interface IStreamSubscription : IAsyncEnumerator<ResolvedEvent> {
				Position CurrentPosition { get; }
			}

			private class CatchupAllSubscription : IStreamSubscription {
				private readonly Guid _subscriptionId;
				private readonly IPublisher _bus;
				private readonly bool _resolveLinks;
				private readonly IEventFilter _eventFilter;
				private readonly IPrincipal _user;
				private readonly CancellationTokenSource _disposedTokenSource;
				private readonly ConcurrentQueueWrapper<ResolvedEvent> _buffer;
				private readonly CancellationTokenRegistration _tokenRegistration;
				private readonly Position _startPosition;

				private Position _nextPosition;
				private ResolvedEvent _current;
				private Position _currentPosition;

				public ResolvedEvent Current => _current;
				public Position CurrentPosition => _currentPosition;

				public CatchupAllSubscription(Guid subscriptionId,
					IPublisher bus,
					Position position,
					bool resolveLinks,
					IEventFilter eventFilter,
					IPrincipal user,
					IReadIndex readIndex,
					CancellationToken cancellationToken) {
					if (bus == null) {
						throw new ArgumentNullException(nameof(bus));
					}

					if (eventFilter == null) {
						throw new ArgumentNullException(nameof(eventFilter));
					}

					if (readIndex == null) {
						throw new ArgumentNullException(nameof(readIndex));
					}

					_subscriptionId = subscriptionId;
					_bus = bus;
					_nextPosition = position == Position.End
						? Position.FromInt64(readIndex.LastIndexedPosition, readIndex.LastIndexedPosition)
						: position;
					_startPosition = position == Position.End ? Position.Start : position;
					_resolveLinks = resolveLinks;
					_eventFilter = eventFilter;
					_user = user;
					_disposedTokenSource = new CancellationTokenSource();
					_buffer = new ConcurrentQueueWrapper<ResolvedEvent>();
					_tokenRegistration = cancellationToken.Register(_disposedTokenSource.Dispose);
					_currentPosition = _startPosition;
					Log.Info("Catch-up subscription {subscriptionId} to $all:{eventFilter} running...",
						_subscriptionId, _eventFilter);
				}

				public ValueTask DisposeAsync() {
					_disposedTokenSource.Dispose();
					_tokenRegistration.Dispose();
					return default;
				}

				public async ValueTask<bool> MoveNextAsync() {
					ReadLoop:
					if (_disposedTokenSource.IsCancellationRequested) {
						return false;
					}

					if (_buffer.TryDequeue(out var current)) {
						_current = current;
						_currentPosition = Position.FromInt64(current.OriginalPosition.Value.CommitPosition,
							current.OriginalPosition.Value.PreparePosition);
						return true;
					}

					var correlationId = Guid.NewGuid();

					var readNextSource = new TaskCompletionSource<bool>();

					var (commitPosition, preparePosition) = _nextPosition.ToInt64();

					Log.Trace(
						"Catch-up subscription {subscriptionId} to $all:{eventFilter} reading next page starting from {nextPosition}.",
						_subscriptionId, _eventFilter, _nextPosition);

					_bus.Publish(new ClientMessage.FilteredReadAllEventsForward(
						correlationId, correlationId, new CallbackEnvelope(OnMessage), commitPosition, preparePosition,
						ReadBatchSize, _resolveLinks, false, 32, default, _eventFilter, _user));

					var isEnd = await readNextSource.Task.ConfigureAwait(false);

					if (_buffer.TryDequeue(out current)) {
						_current = current;
						_currentPosition = Position.FromInt64(current.OriginalPosition.Value.CommitPosition,
							current.OriginalPosition.Value.PreparePosition);
						return true;
					}

					if (isEnd) {
						return false;
					}

					if (_disposedTokenSource.IsCancellationRequested) {
						return false;
					}

					goto ReadLoop;

					void OnMessage(Message message) {
						if (message is ClientMessage.NotHandled notHandled &&
						    RpcExceptions.TryHandleNotHandled(notHandled, out var ex)) {
							readNextSource.TrySetException(ex);
							return;
						}

						if (!(message is ClientMessage.FilteredReadAllEventsForwardCompleted completed)) {
							readNextSource.TrySetException(
								RpcExceptions.UnknownMessage<ClientMessage.FilteredReadAllEventsForwardCompleted>(
									message));
							return;
						}

						switch (completed.Result) {
							case FilteredReadAllResult.Success:
								foreach (var @event in completed.Events) {
									var position = Position.FromInt64(
										@event.OriginalPosition.Value.CommitPosition,
										@event.OriginalPosition.Value.PreparePosition);
									if (position <= _startPosition) {
										Log.Trace(
											"Catch-up subscription {subscriptionId} to $all:{eventFilter} skipped event {position}.",
											_subscriptionId, _eventFilter, position);
										continue;
									}

									Log.Trace(
										"Catch-up subscription {subscriptionId} to $all:{eventFilter} received event {position}.",
										_subscriptionId, _eventFilter, position);

									_buffer.Enqueue(@event);
								}

								_nextPosition = Position.FromInt64(
									completed.NextPos.CommitPosition,
									completed.NextPos.PreparePosition);
								readNextSource.TrySetResult(completed.IsEndOfStream);
								return;
							case FilteredReadAllResult.AccessDenied:
								readNextSource.TrySetException(RpcExceptions.AccessDenied());
								return;
							default:
								readNextSource.TrySetException(RpcExceptions.UnknownError(completed.Result));
								return;
						}
					}
				}
			}

			private class LiveStreamSubscription : IStreamSubscription {
				private readonly ConcurrentQueue<(ResolvedEvent resolvedEvent, Exception exception)>
					_liveEventBuffer;

				private readonly ConcurrentQueue<(ResolvedEvent resolvedEvent, Exception exception)>
					_historicalEventBuffer;

				private readonly Guid _subscriptionId;
				private readonly IPublisher _bus;
				private readonly IEventFilter _eventFilter;
				private readonly IPrincipal _user;
				private readonly TaskCompletionSource<Position> _subscriptionConfirmed;
				private readonly TaskCompletionSource<bool> _readHistoricalEventsCompleted;
				private readonly CancellationTokenRegistration _tokenRegistration;
				private readonly CancellationTokenSource _disposedTokenSource;

				private ResolvedEvent _current;
				private Position _currentPosition;

				public ResolvedEvent Current => _current;
				public Position CurrentPosition => _currentPosition;

				public LiveStreamSubscription(Guid subscriptionId,
					IPublisher bus,
					Position currentPosition,
					bool resolveLinks,
					IEventFilter eventFilter,
					IPrincipal user,
					CancellationToken cancellationToken) {
					if (bus == null) {
						throw new ArgumentNullException(nameof(bus));
					}

					if (eventFilter == null) {
						throw new ArgumentNullException(nameof(eventFilter));
					}

					_liveEventBuffer = new ConcurrentQueueWrapper<(ResolvedEvent resolvedEvent, Exception exception)>();
					_historicalEventBuffer = new ConcurrentQueue<(ResolvedEvent resolvedEvent, Exception exception)>();
					_subscriptionId = subscriptionId;
					_bus = bus;
					_eventFilter = eventFilter;
					_user = user;
					_subscriptionConfirmed = new TaskCompletionSource<Position>();
					_readHistoricalEventsCompleted = new TaskCompletionSource<bool>();
					_disposedTokenSource = new CancellationTokenSource();
					_tokenRegistration = cancellationToken.Register(_disposedTokenSource.Dispose);
					_currentPosition = currentPosition;

					Log.Info("Live subscription {subscriptionId} to $all:{eventFilter} running...",
						_subscriptionId, eventFilter);

					bus.Publish(new ClientMessage.FilteredSubscribeToStream(Guid.NewGuid(), _subscriptionId,
						new CallbackEnvelope(OnSubscriptionMessage), subscriptionId, string.Empty, resolveLinks, user,
						eventFilter, 32));

					void OnSubscriptionMessage(Message message) {
						if (message is ClientMessage.NotHandled notHandled &&
						    RpcExceptions.TryHandleNotHandled(notHandled, out var ex)) {
							_subscriptionConfirmed.TrySetException(ex);
							return;
						}

						switch (message) {
							case ClientMessage.SubscriptionConfirmation confirmed:
								var caughtUp = Position.FromInt64(confirmed.LastIndexedPosition,
									confirmed.LastIndexedPosition);
								Log.Trace(
									"Live subscription {subscriptionId} to $all:{eventFilter} confirmed at {position}.",
									_subscriptionId, _eventFilter, caughtUp);
								_subscriptionConfirmed.TrySetResult(caughtUp);
								ReadHistoricalEvents(currentPosition);

								return;
							case ClientMessage.SubscriptionDropped dropped:
								switch (dropped.Reason) {
									case SubscriptionDropReason.AccessDenied:
										Fail(RpcExceptions.AccessDenied());
										return;
									default:
										Fail(RpcExceptions.UnknownError(dropped.Reason));
										return;
								}
							case ClientMessage.StreamEventAppeared appeared:
								_liveEventBuffer.Enqueue((appeared.Event, null));
								return;
							default:
								Fail(RpcExceptions.UnknownMessage<ClientMessage.SubscriptionConfirmation>(message));
								return;
						}

						void Fail(Exception ex) {
							_liveEventBuffer.Enqueue((default, ex));
							_subscriptionConfirmed.TrySetException(ex);
						}
					}

					void OnHistoricalEventsMessage(Message message) {
						if (message is ClientMessage.NotHandled notHandled &&
						    RpcExceptions.TryHandleNotHandled(notHandled, out var ex)) {
							_readHistoricalEventsCompleted.TrySetException(ex);
							return;
						}

						if (!(message is ClientMessage.FilteredReadAllEventsForwardCompleted completed)) {
							_readHistoricalEventsCompleted.TrySetException(
								RpcExceptions
									.UnknownMessage<ClientMessage.FilteredReadAllEventsForwardCompleted>(message));
							return;
						}

						switch (completed.Result) {
							case FilteredReadAllResult.Success:
								foreach (var @event in completed.Events) {
									var position = Position.FromInt64(
										@event.OriginalPosition.Value.CommitPosition,
										@event.OriginalPosition.Value.PreparePosition);
									if (currentPosition >= position) {
										Log.Trace(
											"Live subscription {subscriptionId} to $all:{eventFilter} skipping missed event at {position}.",
											_subscriptionId, eventFilter, position);
										continue;
									}

									if (position <= _subscriptionConfirmed.Task.Result) {
										Log.Trace(
											"Live subscription {subscriptionId} to $all:{eventFilter} enqueueing missed event at {position}.",
											_subscriptionId, eventFilter, position);
										_historicalEventBuffer.Enqueue((@event, null));
									} else {
										Log.Trace(
											"Live subscription {subscriptionId} to $all:{eventFilter} caught up at {position}.",
											_subscriptionId, eventFilter, position);
										_readHistoricalEventsCompleted.TrySetResult(true);
										return;
									}
								}

								var fromPosition = Position.FromInt64(
									completed.NextPos.CommitPosition,
									completed.NextPos.PreparePosition);
								if (completed.IsEndOfStream) {
									Log.Trace(
										"Live subscription {subscriptionId} to $all:{eventFilter} caught up at {position}.",
										_subscriptionId, eventFilter, fromPosition);
									_readHistoricalEventsCompleted.TrySetResult(true);
									return;
								}

								ReadHistoricalEvents(fromPosition);
								return;
							case FilteredReadAllResult.AccessDenied:
								_readHistoricalEventsCompleted.TrySetException(RpcExceptions.AccessDenied());
								return;
							default:
								_readHistoricalEventsCompleted.TrySetException(
									RpcExceptions.UnknownError(completed.Result));
								return;
						}
					}

					void ReadHistoricalEvents(Position fromPosition) {
						Log.Trace(
							"Live subscription {subscriptionId} to $all:{eventFilter} loading any missed events ending with {fromPosition}.",
							_subscriptionId, eventFilter, fromPosition);

						var correlationId = Guid.NewGuid();
						var (commitPosition, preparePosition) = fromPosition.ToInt64();
						bus.Publish(new ClientMessage.FilteredReadAllEventsForward(correlationId, correlationId,
							new CallbackEnvelope(OnHistoricalEventsMessage), commitPosition, preparePosition,
							ReadBatchSize, resolveLinks, false, 32, null, eventFilter, user));
					}
				}

				public async ValueTask<bool> MoveNextAsync() {
					(ResolvedEvent, Exception) _;

					await Task.WhenAll(_subscriptionConfirmed.Task, _readHistoricalEventsCompleted.Task)
						.ConfigureAwait(false);

					if (_historicalEventBuffer.TryDequeue(out _)) {
						var (historicalEvent, historicalException) = _;

						if (historicalException != null) {
							throw historicalException;
						}

						var position = Position.FromInt64(
							historicalEvent.OriginalPosition.Value.CommitPosition,
							historicalEvent.OriginalPosition.Value.PreparePosition);

						if (_liveEventBuffer.Count > MaxLiveEventBufferCount) {
							Log.Warn("Live subscription {subscriptionId} to $all:{eventFilter} buffer is full.",
								_subscriptionId, _eventFilter);

							_liveEventBuffer.Clear();
							_historicalEventBuffer.Clear();

							return false;
						}

						_current = historicalEvent;
						_currentPosition = position;
						Log.Trace(
							"Live subscription {subscriptionId} to $all:{eventFilter} received event {position} historically.",
							_subscriptionId, _eventFilter, position);
						return true;
					}

					var delay = 1;

					while (!_liveEventBuffer.TryDequeue(out _)) {
						await Task.Delay(Math.Max(delay *= 2, 50), _disposedTokenSource.Token)
							.ConfigureAwait(false);
					}

					var (resolvedEvent, exception) = _;

					if (exception != null) {
						throw exception;
					}

					Log.Trace(
						"Live subscription {subscriptionId} to $all:{eventFilter} received event {position} live.",
						_subscriptionId, _eventFilter, resolvedEvent.OriginalPosition);
					_current = resolvedEvent;
					_currentPosition = Position.FromInt64(resolvedEvent.OriginalPosition.Value.CommitPosition,
						resolvedEvent.OriginalPosition.Value.PreparePosition);
					return true;
				}

				public ValueTask DisposeAsync() {
					Log.Info("Live subscription {subscriptionId} to $all:{eventFilter} disposed.",
						_subscriptionId,
						_eventFilter);
					_bus.Publish(new ClientMessage.UnsubscribeFromStream(Guid.NewGuid(), _subscriptionId,
						new NoopEnvelope(), _user));
					_disposedTokenSource.Dispose();
					_tokenRegistration.Dispose();
					return default;
				}
			}
		}
	}
}
