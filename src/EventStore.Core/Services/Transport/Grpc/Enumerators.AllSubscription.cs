using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Client;

namespace EventStore.Core.Services.Transport.Grpc {
	partial class Enumerators {
		public class AllSubscription : ISubscriptionEnumerator {
			private static readonly ILogger Log = LogManager.GetLoggerFor<StreamSubscription>();

			private readonly Guid _subscriptionId;
			private readonly IPublisher _bus;
			private readonly bool _resolveLinks;
			private readonly IPrincipal _user;
			private readonly IReadIndex _readIndex;
			private readonly CancellationToken _cancellationToken;
			private readonly TaskCompletionSource<bool> _subscriptionStarted;
			private IStreamSubscription _inner;

			public ResolvedEvent Current => _inner.Current;
			public Task Started => _subscriptionStarted.Task;
			public string SubscriptionId => _subscriptionId.ToString();

			public AllSubscription(IPublisher bus,
				Position? startPosition,
				bool resolveLinks,
				IPrincipal user,
				IReadIndex readIndex,
				CancellationToken cancellationToken) {
				if (bus == null) {
					throw new ArgumentNullException(nameof(bus));
				}

				if (readIndex == null) {
					throw new ArgumentNullException(nameof(readIndex));
				}

				_subscriptionId = Guid.NewGuid();
				_bus = bus;
				_resolveLinks = resolveLinks;
				_user = user;
				_readIndex = readIndex;
				_cancellationToken = cancellationToken;
				_subscriptionStarted = new TaskCompletionSource<bool>();
				_subscriptionStarted.SetResult(true);

				_inner = startPosition == Position.End
					? (IStreamSubscription)new LiveStreamSubscription(_subscriptionId, _bus,
						Position.FromInt64(_readIndex.LastIndexedPosition, _readIndex.LastIndexedPosition),
						_resolveLinks, _user, _cancellationToken)
					: new CatchupAllSubscription(_subscriptionId, bus, startPosition ?? Position.Start, resolveLinks,
						user, readIndex, cancellationToken);
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
				Log.Trace("Subscription {subscriptionId} to $all reached the end, switching...",
					_subscriptionId);

				if (_inner is LiveStreamSubscription)
					_inner = new CatchupAllSubscription(_subscriptionId, _bus, currentPosition, _resolveLinks,
						_user, _readIndex, _cancellationToken);
				else
					_inner = new LiveStreamSubscription(_subscriptionId, _bus, currentPosition, _resolveLinks,
						_user, _cancellationToken);

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
				private readonly IPrincipal _user;
				private readonly CancellationTokenSource _disposedTokenSource;
				private readonly ConcurrentQueue<ResolvedEvent> _buffer;
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
					IPrincipal user,
					IReadIndex readIndex,
					CancellationToken cancellationToken) {
					if (bus == null) {
						throw new ArgumentNullException(nameof(bus));
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
					_user = user;
					_disposedTokenSource = new CancellationTokenSource();
					_buffer = new ConcurrentQueue<ResolvedEvent>();
					_tokenRegistration = cancellationToken.Register(_disposedTokenSource.Dispose);
					_currentPosition = _startPosition;
					Log.Info("Catch-up subscription {subscriptionId} to $all running...", _subscriptionId);
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
						"Catch-up subscription {subscriptionId} to $all reading next page starting from {nextPosition}.",
						_subscriptionId, _nextPosition);

					_bus.Publish(new ClientMessage.ReadAllEventsForward(
						correlationId, correlationId, new CallbackEnvelope(OnMessage), commitPosition, preparePosition,
						ReadBatchSize, _resolveLinks, false, default, _user));

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

						if (!(message is ClientMessage.ReadAllEventsForwardCompleted completed)) {
							readNextSource.TrySetException(
								RpcExceptions.UnknownMessage<ClientMessage.ReadAllEventsForwardCompleted>(message));
							return;
						}

						switch (completed.Result) {
							case ReadAllResult.Success:
								foreach (var @event in completed.Events) {
									var position = Position.FromInt64(
										@event.OriginalPosition.Value.CommitPosition,
										@event.OriginalPosition.Value.PreparePosition);
									if (position <= _startPosition) {
										Log.Trace(
											"Catch-up subscription {subscriptionId} to $all skipped event {position}.",
											_subscriptionId, position);
										continue;
									}

									Log.Trace(
										"Catch-up subscription {subscriptionId} to $all received event {position}.",
										_subscriptionId, position);

									_buffer.Enqueue(@event);
								}

								_nextPosition = Position.FromInt64(
									completed.NextPos.CommitPosition,
									completed.NextPos.PreparePosition);
								readNextSource.TrySetResult(completed.IsEndOfStream);
								return;
							case ReadAllResult.AccessDenied:
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
				private readonly IPrincipal _user;
				private readonly TaskCompletionSource<Position> _subscriptionConfirmed;
				private readonly TaskCompletionSource<bool> _readHistoricalEventsCompleted;
				private readonly CancellationTokenRegistration _tokenRegistration;
				private readonly CancellationTokenSource _disposedTokenSource;

				private ResolvedEvent _current;
				private Position _currentPosition;
				private int _maxBufferSizeExceeded;

				private bool MaxBufferSizeExceeded => _maxBufferSizeExceeded > 0;

				public ResolvedEvent Current => _current;
				public Position CurrentPosition => _currentPosition;

				public LiveStreamSubscription(Guid subscriptionId,
					IPublisher bus,
					Position currentPosition,
					bool resolveLinks,
					IPrincipal user,
					CancellationToken cancellationToken) {
					if (bus == null) {
						throw new ArgumentNullException(nameof(bus));
					}

					_liveEventBuffer = new ConcurrentQueue<(ResolvedEvent resolvedEvent, Exception exception)>();
					_historicalEventBuffer = new ConcurrentQueue<(ResolvedEvent resolvedEvent, Exception exception)>();
					_subscriptionId = subscriptionId;
					_bus = bus;
					_user = user;
					_subscriptionConfirmed = new TaskCompletionSource<Position>();
					_readHistoricalEventsCompleted = new TaskCompletionSource<bool>();
					_disposedTokenSource = new CancellationTokenSource();
					_tokenRegistration = cancellationToken.Register(_disposedTokenSource.Dispose);
					_currentPosition = currentPosition;

					Log.Info("Live subscription {subscriptionId} to $all running...", subscriptionId);

					bus.Publish(new ClientMessage.SubscribeToStream(Guid.NewGuid(), _subscriptionId,
						new CallbackEnvelope(OnSubscriptionMessage), subscriptionId, string.Empty, resolveLinks, user));

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
									"Live subscription {subscriptionId} to $all confirmed at {position}.",
									_subscriptionId, caughtUp);
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
								if (MaxBufferSizeExceeded) {
									return;
								}

								if (_liveEventBuffer.Count > MaxLiveEventBufferCount) {
									MaximumBufferSizeExceeded();
									return;
								}

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

						void OnHistoricalEventsMessage(Message message) {
							if (message is ClientMessage.NotHandled notHandled &&
							    RpcExceptions.TryHandleNotHandled(notHandled, out var ex)) {
								_readHistoricalEventsCompleted.TrySetException(ex);
								return;
							}

							if (!(message is ClientMessage.ReadAllEventsForwardCompleted completed)) {
								_readHistoricalEventsCompleted.TrySetException(
									RpcExceptions
										.UnknownMessage<ClientMessage.ReadAllEventsForwardCompleted>(message));
								return;
							}

							switch (completed.Result) {
								case ReadAllResult.Success:
									foreach (var @event in completed.Events) {
										var position = Position.FromInt64(
											@event.OriginalPosition.Value.CommitPosition,
											@event.OriginalPosition.Value.PreparePosition);
										if (currentPosition >= position) {
											Log.Trace(
												"Live subscription {subscriptionId} to $all skipping missed event at {position}.",
												_subscriptionId, position);
											continue;
										}

										if (position <= _subscriptionConfirmed.Task.Result) {
											Log.Trace(
												"Live subscription {subscriptionId} to $all enqueueing missed event at {position}.",
												_subscriptionId, position);
											_historicalEventBuffer.Enqueue((@event, null));
										} else {
											Log.Trace(
												"Live subscription {subscriptionId} to $all caught up at {position}.",
												_subscriptionId, position);
											_readHistoricalEventsCompleted.TrySetResult(true);
											return;
										}
									}

									if (_historicalEventBuffer.Count > MaxLiveEventBufferCount) {
										MaximumBufferSizeExceeded();
										return;
									}

									var fromPosition = Position.FromInt64(
										completed.NextPos.CommitPosition,
										completed.NextPos.PreparePosition);
									if (completed.IsEndOfStream) {
										Log.Trace(
											"Live subscription {subscriptionId} to $all caught up at {position}.",
											_subscriptionId, fromPosition);
										_readHistoricalEventsCompleted.TrySetResult(true);
										return;
									}

									ReadHistoricalEvents(fromPosition);
									return;
								case ReadAllResult.AccessDenied:
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
								"Live subscription {subscriptionId} to $all loading any missed events starting from {position}.",
								subscriptionId, fromPosition);

							var correlationId = Guid.NewGuid();
							var (commitPosition, preparePosition) = fromPosition.ToInt64();
							bus.Publish(new ClientMessage.ReadAllEventsForward(correlationId, correlationId,
								new CallbackEnvelope(OnHistoricalEventsMessage), commitPosition, preparePosition,
								ReadBatchSize, resolveLinks, false, null, user));
						}
					}
				}

				public async ValueTask<bool> MoveNextAsync() {
					(ResolvedEvent, Exception) _;

					await Task.WhenAll(_subscriptionConfirmed.Task, _readHistoricalEventsCompleted.Task)
						.ConfigureAwait(false);

					if (MaxBufferSizeExceeded) {
						return false;
					}

					if (_historicalEventBuffer.TryDequeue(out _)) {
						var (historicalEvent, historicalException) = _;

						if (historicalException != null) {
							throw historicalException;
						}

						var position = Position.FromInt64(
							historicalEvent.OriginalPosition.Value.CommitPosition,
							historicalEvent.OriginalPosition.Value.PreparePosition);

						_current = historicalEvent;
						_currentPosition = position;
						Log.Trace(
							"Live subscription {subscriptionId} to $all received event {position} historically.",
							_subscriptionId, position);
						return true;
					}

					var delay = 1;

					while (!_liveEventBuffer.TryDequeue(out _)) {
						if (MaxBufferSizeExceeded) {
							return false;
						}

						await Task.Delay(Math.Max(delay *= 2, 50), _disposedTokenSource.Token)
							.ConfigureAwait(false);
					}

					var (resolvedEvent, exception) = _;

					if (exception != null) {
						throw exception;
					}

					Log.Trace("Live subscription {subscriptionId} to $all received event {position} live.",
						_subscriptionId, resolvedEvent.OriginalPosition);
					_current = resolvedEvent;
					_currentPosition = Position.FromInt64(resolvedEvent.OriginalPosition.Value.CommitPosition,
						resolvedEvent.OriginalPosition.Value.PreparePosition);
					return true;
				}

				private void MaximumBufferSizeExceeded() {
					Interlocked.Exchange(ref _maxBufferSizeExceeded, 1);
					Log.Warn("Live subscription {subscriptionId} to $all buffer is full.",
						_subscriptionId);

					_liveEventBuffer.Clear();
					_historicalEventBuffer.Clear();
				}

				public ValueTask DisposeAsync() {
					Log.Info("Live subscription {subscriptionId} to $all disposed.", _subscriptionId);
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
