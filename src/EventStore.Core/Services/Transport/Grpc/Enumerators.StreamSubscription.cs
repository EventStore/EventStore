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
using EventStore.Grpc;
using IReadIndex = EventStore.Core.Services.Storage.ReaderIndex.IReadIndex;

namespace EventStore.Core.Services.Transport.Grpc {
	internal static partial class Enumerators {
		public class StreamSubscription : IAsyncEnumerator<ResolvedEvent> {
			private readonly IPublisher _bus;
			private readonly string _streamName;
			private readonly bool _resolveLinks;
			private readonly IPrincipal _user;
			private readonly IReadIndex _readIndex;
			private readonly CancellationToken _cancellationToken;
			private IAsyncEnumerator<ResolvedEvent> _inner;
			private readonly Guid _connectionId;

			private bool _catchUpRestarted;

			public ResolvedEvent Current => _inner.Current;

			private StreamRevision CurrentRevision => StreamRevision.FromInt64(Current.OriginalEvent.EventNumber);

			public StreamSubscription(
				IPublisher bus,
				string streamName,
				StreamRevision startRevision,
				bool resolveLinks,
				IPrincipal user,
				IReadIndex readIndex,
				CancellationToken cancellationToken) {
				if (bus == null) {
					throw new ArgumentNullException(nameof(bus));
				}

				if (streamName == null) {
					throw new ArgumentNullException(nameof(streamName));
				}

				if (readIndex == null) {
					throw new ArgumentNullException(nameof(readIndex));
				}

				_bus = bus;
				_streamName = streamName;
				_user = user;
				_readIndex = readIndex;
				_cancellationToken = cancellationToken;
				_resolveLinks = resolveLinks;
				_connectionId = Guid.NewGuid();

				_inner = new CatchupStreamSubscription(bus, streamName, startRevision, resolveLinks, user, readIndex,
					cancellationToken);
			}

			public async ValueTask<bool> MoveNextAsync() {
				var result = await _inner.MoveNextAsync().ConfigureAwait(false);
				if (result) {
					return true;
				}

				if (!_catchUpRestarted) {
					await _inner.DisposeAsync().ConfigureAwait(false);

					_inner = new LiveStreamSubscription(_bus, _connectionId, _streamName,
						CurrentRevision, _resolveLinks, _user,
						OnLiveSubscriptionDropped, _cancellationToken);
				} else {
					_catchUpRestarted = true;
				}

				return await _inner.MoveNextAsync().ConfigureAwait(false);
			}

			public ValueTask DisposeAsync() => _inner.DisposeAsync();

			private async ValueTask OnLiveSubscriptionDropped(StreamRevision caughtUpRevision) {
				await _inner.DisposeAsync().ConfigureAwait(false);

				_inner = new CatchupStreamSubscription(_bus, _streamName, caughtUpRevision, _resolveLinks, _user,
					_readIndex, _cancellationToken);
			}

			private class CatchupStreamSubscription : IAsyncEnumerator<ResolvedEvent> {
				private readonly IPublisher _bus;
				private readonly string _streamName;
				private readonly bool _resolveLinks;
				private readonly IPrincipal _user;
				private readonly IReadIndex _readIndex;
				private readonly CancellationTokenSource _disposedTokenSource;
				private readonly ConcurrentQueue<ResolvedEvent> _buffer;
				private readonly CancellationTokenRegistration _tokenRegistration;
				private StreamRevision _nextRevision;
				private ResolvedEvent _current;

				public ResolvedEvent Current => _current;

				public CatchupStreamSubscription(
					IPublisher bus,
					string streamName,
					StreamRevision startRevision,
					bool resolveLinks,
					IPrincipal user,
					IReadIndex readIndex,
					CancellationToken cancellationToken) {
					if (bus == null) {
						throw new ArgumentNullException(nameof(bus));
					}

					if (streamName == null) {
						throw new ArgumentNullException(nameof(streamName));
					}

					if (readIndex == null) {
						throw new ArgumentNullException(nameof(readIndex));
					}

					_bus = bus;
					_streamName = streamName;
					_nextRevision = startRevision;
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

					var nextRevision = _nextRevision == StreamRevision.End
						? Math.Max(_readIndex.GetStreamLastEventNumber(_streamName), 0L)
						: _nextRevision.ToInt64();

					_bus.Publish(new ClientMessage.ReadStreamEventsForward(
						correlationId, correlationId, new CallbackEnvelope(OnMessage), _streamName, nextRevision, 32,
						_resolveLinks, false, default, _user));

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

						if (!(message is ClientMessage.ReadStreamEventsForwardCompleted completed)) {
							readNextSource.TrySetException(
								RpcExceptions.UnknownMessage<ClientMessage.ReadStreamEventsForwardCompleted>(message));
							return;
						}

						switch (completed.Result) {
							case ReadStreamResult.Success:
								foreach (var @event in completed.Events) {
									_buffer.Enqueue(@event);
								}

								_nextRevision = StreamRevision.FromInt64(completed.NextEventNumber);
								readNextSource.TrySetResult(completed.IsEndOfStream);
								return;
							case ReadStreamResult.NoStream:
								readNextSource.TrySetException(RpcExceptions.StreamNotFound(_streamName));
								return;
							case ReadStreamResult.StreamDeleted:
								readNextSource.TrySetException(RpcExceptions.StreamDeleted(_streamName));
								return;
							case ReadStreamResult.AccessDenied:
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
				private readonly StreamRevision _currentRevision;
				private readonly Func<StreamRevision, ValueTask> _onDropped;
				private readonly TaskCompletionSource<bool> _subscriptionStartedSource;
				private readonly TaskCompletionSource<bool> _readHistoricalStartedSource;
				private readonly CancellationTokenRegistration _tokenRegistration;
				private readonly CancellationTokenSource _disposedTokenSource;

				private ResolvedEvent _current;
				private bool _historicalEventsRead;

				public LiveStreamSubscription(IPublisher bus,
					Guid connectionId,
					string streamName,
					StreamRevision currentRevision,
					bool resolveLinks,
					IPrincipal user,
					Func<StreamRevision, ValueTask> onDropped,
					CancellationToken cancellationToken) {
					if (bus == null) {
						throw new ArgumentNullException(nameof(bus));
					}

					if (streamName == null) {
						throw new ArgumentNullException(nameof(streamName));
					}

					if (onDropped == null) {
						throw new ArgumentNullException(nameof(onDropped));
					}

					_liveEventBuffer = new ConcurrentQueueWrapper<(ResolvedEvent resolvedEvent, Exception exception)>();
					_historicalEventBuffer =
						new ConcurrentQueueWrapper<(ResolvedEvent resolvedEvent, Exception exception)>();
					_currentRevision = currentRevision;
					_onDropped = onDropped;
					_subscriptionStartedSource = new TaskCompletionSource<bool>();
					_readHistoricalStartedSource = new TaskCompletionSource<bool>();
					_disposedTokenSource = new CancellationTokenSource();
					_tokenRegistration = cancellationToken.Register(_disposedTokenSource.Dispose);

					var correlationId = Guid.NewGuid();

					bus.Publish(new ClientMessage.SubscribeToStream(correlationId, correlationId,
						new CallbackEnvelope(OnSubscriptionMessage), connectionId, streamName, resolveLinks, user));

					ReadHistoricalEvents(currentRevision);

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
									case SubscriptionDropReason.NotFound:
										Fail(RpcExceptions.StreamNotFound(streamName));
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

						if (!(message is ClientMessage.ReadStreamEventsForwardCompleted completed)) {
							_readHistoricalStartedSource.TrySetException(
								RpcExceptions.UnknownMessage<ClientMessage.ReadStreamEventsForwardCompleted>(message));
							return;
						}

						switch (completed.Result) {
							case ReadStreamResult.Success:
								foreach (var @event in completed.Events) {
									_historicalEventBuffer.Enqueue((@event, null));
								}

								_readHistoricalStartedSource.TrySetResult(true);
								ReadHistoricalEvents(
									StreamRevision.FromInt64(completed.Events[^1].OriginalEvent.EventNumber));
								return;
							case ReadStreamResult.NoStream:
								_readHistoricalStartedSource.TrySetException(RpcExceptions.StreamNotFound(streamName));
								return;
							case ReadStreamResult.StreamDeleted:
								_readHistoricalStartedSource.TrySetException(RpcExceptions.StreamDeleted(streamName));
								return;
							case ReadStreamResult.AccessDenied:
								_readHistoricalStartedSource.TrySetException(RpcExceptions.AccessDenied());
								return;
							default:
								_readHistoricalStartedSource.TrySetException(
									RpcExceptions.UnknownError(completed.Result));
								return;
						}
					}

					void ReadHistoricalEvents(StreamRevision fromStreamRevision) {
						var correlationId = Guid.NewGuid();
						bus.Publish(new ClientMessage.ReadStreamEventsForward(correlationId, correlationId,
							new CallbackEnvelope(OnHistoricalEventsMessage), streamName, fromStreamRevision.ToInt64(),
							32, resolveLinks, false, null, user));
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

						var streamRevision = StreamRevision.FromInt64(historicalEvent.OriginalEvent.EventNumber);

						if (_liveEventBuffer.Count > 512) {
							_liveEventBuffer.Clear();
							await _onDropped(streamRevision).ConfigureAwait(false);
							return false;
						}

						if (streamRevision < _currentRevision) {
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
