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
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Grpc;

namespace EventStore.Core.Services.Transport.Grpc {
	partial class Enumerators {
		public class AllSubscription : IAsyncEnumerator<ResolvedEvent> {
			private readonly IPublisher _bus;
			private readonly bool _resolveLinks;
			private readonly IPrincipal _user;
			private readonly IReadIndex _readIndex;
			private readonly CancellationToken _cancellationToken;
			private IAsyncEnumerator<ResolvedEvent> _inner;
			private readonly Guid _connectionId;

			private bool _catchUpRestarted;

			public ResolvedEvent Current => _inner.Current;

			private Position CurrentPosition {
				get {
					var position = _inner.Current.OriginalPosition.Value;

					return Position.FromInt64(position.CommitPosition, position.PreparePosition);
				}
			}

			public AllSubscription(IPublisher bus,
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

				_bus = bus;
				_resolveLinks = resolveLinks;
				_user = user;
				_readIndex = readIndex;
				_cancellationToken = cancellationToken;
				_connectionId = Guid.NewGuid();

				_inner = new CatchupAllSubscription(bus, position, resolveLinks, user, readIndex, cancellationToken);
			}

			public async ValueTask<bool> MoveNextAsync() {
				var result = await _inner.MoveNextAsync().ConfigureAwait(false);
				if (result) {
					return true;
				}

				if (!_catchUpRestarted) {
					await _inner.DisposeAsync().ConfigureAwait(false);
					_inner = new LiveStreamSubscription(_bus, OnLiveSubscriptionDropped, _connectionId, CurrentPosition,
						_resolveLinks, _user, _cancellationToken);
				} else {
					_catchUpRestarted = true;
				}

				return await _inner.MoveNextAsync().ConfigureAwait(false);
			}

			public ValueTask DisposeAsync() => _inner.DisposeAsync();

			private async ValueTask OnLiveSubscriptionDropped(Position caughtUpPosition) {
				await _inner.DisposeAsync().ConfigureAwait(false);

				_inner = new CatchupAllSubscription(_bus, caughtUpPosition, _resolveLinks, _user, _readIndex,
					_cancellationToken);
			}

			private class CatchupAllSubscription : IAsyncEnumerator<ResolvedEvent> {
				private readonly IPublisher _bus;
				private readonly bool _resolveLinks;
				private readonly IPrincipal _user;
				private readonly IReadIndex _readIndex;
				private readonly CancellationTokenSource _disposedTokenSource;
				private readonly ConcurrentQueue<ResolvedEvent> _buffer;
				private readonly CancellationTokenRegistration _tokenRegistration;
				private Position _nextPosition;
				private ResolvedEvent _current;

				public ResolvedEvent Current => _current;

				public CatchupAllSubscription(IPublisher bus,
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

					_bus = bus;
					_nextPosition = position;
					_resolveLinks = resolveLinks;
					_user = user;
					_readIndex = readIndex;
					_disposedTokenSource = new CancellationTokenSource();
					_buffer = new ConcurrentQueue<ResolvedEvent>();
					_tokenRegistration = cancellationToken.Register(_disposedTokenSource.Dispose);
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
						return true;
					}

					var correlationId = Guid.NewGuid();

					var readNextSource = new TaskCompletionSource<bool>();

					var (commitPosition, preparePosition) = _nextPosition == Position.End
						? (_readIndex.LastCommitPosition, _readIndex.LastReplicatedPosition)
						: _nextPosition.ToInt64();

					_bus.Publish(new ClientMessage.ReadAllEventsForward(
						correlationId, correlationId, new CallbackEnvelope(OnMessage), commitPosition, preparePosition,
						32, _resolveLinks, false, default, _user));

					var isEnd = await readNextSource.Task.ConfigureAwait(false);

					if (_buffer.TryDequeue(out current)) {
						_current = current;
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

			private class LiveStreamSubscription : IAsyncEnumerator<ResolvedEvent> {
				private readonly ConcurrentQueue<(ResolvedEvent resolvedEvent, Exception exception)>
					_historicalEventBuffer;

				private readonly ConcurrentQueue<(ResolvedEvent resolvedEvent, Exception exception)> _liveEventBuffer;
				private readonly Func<Position, ValueTask> _onDropped;
				private readonly Position _position;
				private readonly TaskCompletionSource<bool> _subscriptionStartedSource;
				private readonly TaskCompletionSource<bool> _readHistoricalStartedSource;
				private readonly CancellationTokenRegistration _tokenRegistration;
				private readonly CancellationTokenSource _disposedTokenSource;

				private ResolvedEvent _current;
				private bool _historicalEventsRead;

				public LiveStreamSubscription(IPublisher bus,
					Func<Position, ValueTask> onDropped,
					Guid connectionId,
					Position position,
					bool resolveLinks,
					IPrincipal user,
					CancellationToken cancellationToken) {
					if (bus == null) {
						throw new ArgumentNullException(nameof(bus));
					}

					if (onDropped == null) {
						throw new ArgumentNullException(nameof(onDropped));
					}

					_liveEventBuffer = new ConcurrentQueueWrapper<(ResolvedEvent resolvedEvent, Exception exception)>();
					_historicalEventBuffer =
						new ConcurrentQueueWrapper<(ResolvedEvent resolvedEvent, Exception exception)>();
					_onDropped = onDropped;
					_position = position;
					_subscriptionStartedSource = new TaskCompletionSource<bool>();
					_readHistoricalStartedSource = new TaskCompletionSource<bool>();
					_disposedTokenSource = new CancellationTokenSource();
					_tokenRegistration = cancellationToken.Register(_disposedTokenSource.Dispose);

					var correlationId = Guid.NewGuid();

					bus.Publish(new ClientMessage.SubscribeToStream(correlationId, correlationId,
						new CallbackEnvelope(OnSubscriptionMessage), connectionId, string.Empty, resolveLinks, user));

					ReadHistoricalEvents(position);

					void OnSubscriptionMessage(Message message) {
						if (message is ClientMessage.NotHandled notHandled &&
						    RpcExceptions.TryHandleNotHandled(notHandled, out var ex)) {
							_subscriptionStartedSource.TrySetException(ex);
							return;
						}

						switch (message) {
							case ClientMessage.SubscriptionConfirmation _:
								_subscriptionStartedSource.TrySetResult(true);
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
								_subscriptionStartedSource.TrySetException(
									RpcExceptions.UnknownMessage<ClientMessage.SubscriptionConfirmation>(message));
								return;
						}

						void Fail(Exception ex) {
							_liveEventBuffer.Enqueue((default, ex));
							_subscriptionStartedSource.TrySetException(ex);
						}
					}

					void OnHistoricalEventsMessage(Message message) {
						if (message is ClientMessage.NotHandled notHandled &&
						    RpcExceptions.TryHandleNotHandled(notHandled, out var ex)) {
							_readHistoricalStartedSource.TrySetException(ex);
							return;
						}

						if (!(message is ClientMessage.ReadAllEventsForwardCompleted completed)) {
							_readHistoricalStartedSource.TrySetException(
								RpcExceptions.UnknownMessage<ClientMessage.ReadStreamEventsForwardCompleted>(message));
							return;
						}

						switch (completed.Result) {
							case ReadAllResult.Success:
								foreach (var @event in completed.Events) {
									_historicalEventBuffer.Enqueue((@event, null));
								}

								_readHistoricalStartedSource.TrySetResult(true);
								var tfPos = completed.Events[^1].OriginalPosition.Value;
								var position = Position.FromInt64(
									tfPos.CommitPosition,
									tfPos.PreparePosition);
								ReadHistoricalEvents(position);
								return;
							case ReadAllResult.AccessDenied:
								_readHistoricalStartedSource.TrySetException(RpcExceptions.AccessDenied());
								return;
							default:
								_readHistoricalStartedSource.TrySetException(
									RpcExceptions.UnknownError(completed.Result));
								return;
						}
					}

					void ReadHistoricalEvents(Position position) {
						var correlationId = Guid.NewGuid();
						var (commitPosition, preparePosition) = position.ToInt64();
						bus.Publish(new ClientMessage.ReadAllEventsForward(correlationId, correlationId,
							new CallbackEnvelope(OnHistoricalEventsMessage), commitPosition, preparePosition, 32,
							resolveLinks, false, null, user));
					}
				}

				public ResolvedEvent Current => _current;

				public async ValueTask<bool> MoveNextAsync() {
					(ResolvedEvent, Exception) _;

					await Task.WhenAll(_subscriptionStartedSource.Task, _readHistoricalStartedSource.Task)
						.ConfigureAwait(false);

					if (!_historicalEventsRead) {
						while (!_historicalEventBuffer.TryDequeue(out _)) {
							await Task.Delay(1, _disposedTokenSource.Token).ConfigureAwait(false);
						}

						var (historicalEvent, historicalException) = _;

						if (historicalException != null) {
							throw historicalException;
						}

						var position = Position.FromInt64(historicalEvent.OriginalPosition.Value.CommitPosition,
							historicalEvent.OriginalPosition.Value.PreparePosition);

						if (_liveEventBuffer.Count > MaxLiveEventBufferCount) {
							_liveEventBuffer.Clear();
							await _onDropped(position).ConfigureAwait(false);
							return false;
						}

						if (position < _position) {
							_current = historicalEvent;
							return true;
						}

						_historicalEventBuffer.Clear();
						_historicalEventsRead = true;
					}

					while (!_liveEventBuffer.TryDequeue(out _)) {
						await Task.Delay(1, _disposedTokenSource.Token).ConfigureAwait(false);
					}

					var (resolvedEvent, exception) = _;

					if (exception != null) {
						throw exception;
					}

					_current = resolvedEvent;
					return true;
				}

				public ValueTask DisposeAsync() {
					_disposedTokenSource.Dispose();
					_tokenRegistration.Dispose();
					return default;
				}
			}
		}
	}
}
