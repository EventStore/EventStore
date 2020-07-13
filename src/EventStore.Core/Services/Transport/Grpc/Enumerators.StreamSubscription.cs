using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Client;
using EventStore.Core.TransactionLog.Services;
using Serilog;
using IReadIndex = EventStore.Core.Services.Storage.ReaderIndex.IReadIndex;

namespace EventStore.Core.Services.Transport.Grpc {
	internal static partial class Enumerators {
		public class StreamSubscription : ISubscriptionEnumerator {
			private static readonly ILogger Log = Serilog.Log.ForContext<StreamSubscription>();

			private readonly Guid _subscriptionId;
			private readonly IPublisher _bus;
			private readonly string _streamName;
			private readonly bool _resolveLinks;
			private readonly ClaimsPrincipal _user;
			private readonly bool _requiresLeader;
			private readonly IReadIndex _readIndex;
			private readonly CancellationToken _cancellationToken;
			private readonly TaskCompletionSource<bool> _subscriptionStarted;
			private readonly StreamRevision _startRevision;
			private IStreamEnumerator _inner;

			public ResolvedEvent Current => _inner.Current;
			public Task Started => _subscriptionStarted.Task;
			public string SubscriptionId => _subscriptionId.ToString();

			public StreamSubscription(
				IPublisher bus,
				string streamName,
				StreamRevision? startRevision,
				bool resolveLinks,
				ClaimsPrincipal user,
				bool requiresLeader,
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

				_subscriptionId = Guid.NewGuid();
				_bus = bus;
				_streamName = streamName;
				_resolveLinks = resolveLinks;
				_user = user;
				_requiresLeader = requiresLeader;
				_readIndex = readIndex;
				_cancellationToken = cancellationToken;
				_subscriptionStarted = new TaskCompletionSource<bool>();

				_startRevision = startRevision == StreamRevision.End
					? StreamRevision.FromInt64(readIndex.GetStreamLastEventNumber(_streamName) + 1)
					: startRevision + 1 ?? StreamRevision.Start;

				_inner = startRevision == StreamRevision.End
					? (IStreamEnumerator)new LiveStreamSubscription(_subscriptionId, _bus, _streamName,
						StreamRevision.FromInt64(readIndex.GetStreamLastEventNumber(_streamName) + 1), _resolveLinks,
						_user, _requiresLeader, _subscriptionStarted, _cancellationToken)
					: new CatchupStreamSubscription(_subscriptionId, bus, streamName,
						startRevision + 1 ?? StreamRevision.Start, resolveLinks, user, _requiresLeader, readIndex,
						_subscriptionStarted, cancellationToken);
			}

			public async ValueTask<bool> MoveNextAsync() {
				ReadLoop:
				if (await _inner.MoveNextAsync().ConfigureAwait(false)) {
					if (_inner.CurrentStreamRevision >= _startRevision)
						return true;
					goto ReadLoop;
				}

				if (_cancellationToken.IsCancellationRequested) {
					return false;
				}

				await _inner.DisposeAsync().ConfigureAwait(false);
				var currentStreamRevision = _inner.CurrentStreamRevision;
				Log.Verbose(
					"Subscription {subscriptionId} to {streamName} reached the end at {streamRevision}, switching...",
					_subscriptionId, _streamName, currentStreamRevision);

				if (_inner is LiveStreamSubscription)
					_inner = new CatchupStreamSubscription(_subscriptionId, _bus, _streamName,
						currentStreamRevision, _resolveLinks, _user, _requiresLeader, _readIndex, _subscriptionStarted,
						_cancellationToken);
				else
					_inner = new LiveStreamSubscription(_subscriptionId, _bus, _streamName, currentStreamRevision,
						_resolveLinks, _user, _requiresLeader, _subscriptionStarted, _cancellationToken);

				goto ReadLoop;
			}


			public ValueTask DisposeAsync() => _inner.DisposeAsync();

			private interface IStreamEnumerator : IAsyncEnumerator<ResolvedEvent> {
				StreamRevision CurrentStreamRevision { get; }
			}

			private class CatchupStreamSubscription : IStreamEnumerator {
				private readonly Guid _subscriptionId;
				private readonly IPublisher _bus;
				private readonly string _streamName;
				private readonly bool _resolveLinks;
				private readonly ClaimsPrincipal _user;
				private readonly bool _requiresLeader;
				private readonly CancellationTokenSource _disposedTokenSource;
				private readonly ConcurrentQueue<ResolvedEvent> _buffer;
				private readonly CancellationTokenRegistration _tokenRegistration;
				private readonly StreamRevision _startRevision;

				private StreamRevision _nextRevision;
				private ResolvedEvent _current;
				private StreamRevision _currentStreamRevision;

				public ResolvedEvent Current => _current;
				public StreamRevision CurrentStreamRevision => _currentStreamRevision;

				public CatchupStreamSubscription(Guid subscriptionId,
					IPublisher bus,
					string streamName,
					StreamRevision startRevision,
					bool resolveLinks,
					ClaimsPrincipal user,
					bool requiresLeader,
					IReadIndex readIndex,
					TaskCompletionSource<bool> subscriptionStarted,
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

					if (subscriptionStarted == null) {
						throw new ArgumentNullException(nameof(subscriptionStarted));
					}

					_subscriptionId = subscriptionId;
					_bus = bus;
					_streamName = streamName;
					_nextRevision = startRevision == StreamRevision.End
						? StreamRevision.FromInt64(readIndex.GetStreamLastEventNumber(_streamName) + 1)
						: startRevision;
					_startRevision = startRevision == StreamRevision.End ? StreamRevision.Start : startRevision;
					_resolveLinks = resolveLinks;
					_user = user;
					_requiresLeader = requiresLeader;
					var subscriptionStarted1 = subscriptionStarted;
					_disposedTokenSource = new CancellationTokenSource();
					_buffer = new ConcurrentQueue<ResolvedEvent>();
					_tokenRegistration = cancellationToken.Register(_disposedTokenSource.Dispose);

					if (!subscriptionStarted1.Task.IsCompleted) {
						subscriptionStarted1.SetResult(true);
					}

					Log.Information(
						"Catch-up subscription {subscriptionId} to {streamName}@{streamRevision} running...",
						_subscriptionId, streamName, _nextRevision);
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
						_currentStreamRevision = StreamRevision.FromInt64(current.OriginalEventNumber);
						return true;
					}

					var readNextSource = new TaskCompletionSource<bool>();

					Guid correlationId = Guid.NewGuid();
					Log.Verbose(
						"Catch-up subscription {subscriptionId} to {streamName} reading next page starting from {nextRevision}.",
						_subscriptionId, _streamName, _nextRevision);

					_bus.Publish(new ClientMessage.ReadStreamEventsForward(
						correlationId, correlationId, new CallbackEnvelope(OnMessage), _streamName,
						_nextRevision.ToInt64(), ReadBatchSize, _resolveLinks, _requiresLeader, default, _user));

					var isEnd = await readNextSource.Task.ConfigureAwait(false);

					if (_buffer.TryDequeue(out current)) {
						_current = current;
						_currentStreamRevision = StreamRevision.FromInt64(current.OriginalEvent.EventNumber);
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
									var streamRevision = StreamRevision.FromInt64(@event.OriginalEvent.EventNumber);
									if (streamRevision < _startRevision) {
										Log.Verbose(
											"Catch-up subscription {subscriptionId} to {streamName} skipped event {streamRevision}.",
											_subscriptionId, _streamName, streamRevision);
										continue;
									}

									Log.Verbose(
										"Catch-up subscription {subscriptionId} to {streamName} received event {streamRevision}.",
										_subscriptionId, _streamName, streamRevision);

									_buffer.Enqueue(@event);
								}

								_nextRevision = StreamRevision.FromInt64(completed.NextEventNumber);
								readNextSource.TrySetResult(completed.IsEndOfStream);
								return;
							case ReadStreamResult.NoStream:
								readNextSource.TrySetResult(true);
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

			private class LiveStreamSubscription : IStreamEnumerator {
				private readonly ConcurrentQueue<(ResolvedEvent resolvedEvent, Exception exception)>
					_liveEventBuffer;

				private readonly ConcurrentQueue<(ResolvedEvent resolvedEvent, Exception exception)>
					_historicalEventBuffer;

				private readonly Guid _subscriptionId;
				private readonly IPublisher _bus;
				private readonly string _streamName;
				private readonly ClaimsPrincipal _user;
				private readonly bool _requiresLeader;
				private readonly TaskCompletionSource<StreamRevision> _subscriptionConfirmed;
				private readonly TaskCompletionSource<bool> _readHistoricalEventsCompleted;
				private readonly CancellationTokenRegistration _tokenRegistration;
				private readonly CancellationTokenSource _disposedTokenSource;

				private ResolvedEvent _current;
				private StreamRevision _currentStreamRevision;
				private int _maxBufferSizeExceeded;

				private bool MaxBufferSizeExceeded => _maxBufferSizeExceeded > 0;

				public ResolvedEvent Current => _current;
				public StreamRevision CurrentStreamRevision => _currentStreamRevision;

				public LiveStreamSubscription(Guid subscriptionId,
					IPublisher bus,
					string streamName,
					StreamRevision currentStreamRevision,
					bool resolveLinks,
					ClaimsPrincipal user,
					bool requiresLeader,
					TaskCompletionSource<bool> subscriptionStarted,
					CancellationToken cancellationToken) {
					if (bus == null) {
						throw new ArgumentNullException(nameof(bus));
					}

					if (streamName == null) {
						throw new ArgumentNullException(nameof(streamName));
					}

					if (subscriptionStarted == null) {
						throw new ArgumentNullException(nameof(subscriptionStarted));
					}

					if (currentStreamRevision == StreamRevision.End) {
						throw new ArgumentOutOfRangeException(nameof(currentStreamRevision));
					}

					_liveEventBuffer = new ConcurrentQueue<(ResolvedEvent resolvedEvent, Exception exception)>();
					_historicalEventBuffer = new ConcurrentQueue<(ResolvedEvent resolvedEvent, Exception exception)>();
					_subscriptionId = subscriptionId;
					_bus = bus;
					_streamName = streamName;
					_user = user;
					_requiresLeader = requiresLeader;
					_subscriptionConfirmed = new TaskCompletionSource<StreamRevision>();
					_readHistoricalEventsCompleted = new TaskCompletionSource<bool>();
					_disposedTokenSource = new CancellationTokenSource();
					_tokenRegistration = cancellationToken.Register(_disposedTokenSource.Dispose);
					_currentStreamRevision = currentStreamRevision;

					Log.Information(
						"Live subscription {subscriptionId} to {streamName} running from {streamRevision}...",
						subscriptionId, streamName, currentStreamRevision);

					bus.Publish(new ClientMessage.SubscribeToStream(Guid.NewGuid(), _subscriptionId,
						new CallbackEnvelope(OnSubscriptionMessage), subscriptionId, streamName, resolveLinks, user));

					void OnSubscriptionMessage(Message message) {
						if (!subscriptionStarted.Task.IsCompleted) {
							subscriptionStarted.SetResult(true);
						}

						if (message is ClientMessage.NotHandled notHandled &&
						    RpcExceptions.TryHandleNotHandled(notHandled, out var ex)) {
							_subscriptionConfirmed.TrySetException(ex);
							return;
						}

						switch (message) {
							case ClientMessage.SubscriptionConfirmation confirmed:
								var caughtUp = StreamRevision.FromInt64(Math.Max(confirmed.LastEventNumber.Value + 1,
									currentStreamRevision.ToInt64()));
								Log.Verbose(
									"Live subscription {subscriptionId} to {streamName} confirmed at {streamRevision}.",
									_subscriptionId, _streamName, caughtUp);
								_subscriptionConfirmed.TrySetResult(caughtUp);
								subscriptionStarted.TrySetResult(true);
								ReadHistoricalEvents(currentStreamRevision);

								return;
							case ClientMessage.SubscriptionDropped dropped:
								Log.Debug(
									"Live subscription {subscriptionId} to {streamName} dropped: {droppedReason}",
									_subscriptionId, _streamName, dropped.Reason);
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
								if (MaxBufferSizeExceeded) {
									return;
								}

								if (_liveEventBuffer.Count > MaxLiveEventBufferCount) {
									MaximumBufferSizeExceeded();
									return;
								}

								if (appeared.Event.OriginalEvent.EventType == SystemEventTypes.StreamDeleted) {
									_liveEventBuffer.Enqueue((default, RpcExceptions.StreamDeleted(_streamName)));
									return;
								}

								var streamRevision = StreamRevision.FromInt64(appeared.Event.OriginalEventNumber);
								if (currentStreamRevision > streamRevision) {
									Log.Verbose(
										"Live subscription {subscriptionId} to {streamName} skipped event {streamRevision}.",
										_subscriptionId, _streamName, streamRevision);
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
					}

					void OnHistoricalEventsMessage(Message message) {
						if (message is ClientMessage.NotHandled notHandled &&
						    RpcExceptions.TryHandleNotHandled(notHandled, out var ex)) {
							_readHistoricalEventsCompleted.TrySetException(ex);
							return;
						}

						if (!(message is ClientMessage.ReadStreamEventsForwardCompleted completed)) {
							_readHistoricalEventsCompleted.TrySetException(
								RpcExceptions.UnknownMessage<ClientMessage.ReadStreamEventsForwardCompleted>(message));
							return;
						}

						switch (completed.Result) {
							case ReadStreamResult.Success:
								foreach (var @event in completed.Events) {
									var streamRevision = StreamRevision.FromInt64(@event.OriginalEvent.EventNumber);
									if (streamRevision < currentStreamRevision) {
										Log.Verbose(
											"Live subscription {subscriptionId} to {streamName} skipping missed event at {streamRevision}.",
											_subscriptionId, streamName, streamRevision);
										continue;
									}

									if (streamRevision < _subscriptionConfirmed.Task.Result &&
									    streamRevision > currentStreamRevision) {
										Log.Verbose(
											"Live subscription {subscriptionId} to {streamName} enqueueing missed event at {streamRevision}.",
											_subscriptionId, streamName, streamRevision);
										_historicalEventBuffer.Enqueue((@event, null));
									} else {
										Log.Verbose(
											"Live subscription {subscriptionId} to {streamName} caught up at {streamRevision}.",
											_subscriptionId, streamName, streamRevision);
										_readHistoricalEventsCompleted.TrySetResult(true);
										return;
									}
								}

								if (_historicalEventBuffer.Count > MaxLiveEventBufferCount) {
									MaximumBufferSizeExceeded();
									return;
								}

								var fromStreamRevision = StreamRevision.FromInt64(completed.NextEventNumber);
								if (completed.IsEndOfStream) {
									Log.Verbose(
										"Live subscription {subscriptionId} to {streamName} caught up at {streamRevision}.",
										_subscriptionId, streamName, fromStreamRevision);
									_readHistoricalEventsCompleted.TrySetResult(true);
									return;
								}

								ReadHistoricalEvents(fromStreamRevision);
								return;
							case ReadStreamResult.NoStream:
								Log.Verbose("Live subscription {subscriptionId} to {streamName} stream not found.",
									_subscriptionId, _streamName);
								_readHistoricalEventsCompleted.TrySetResult(true);
								return;
							case ReadStreamResult.StreamDeleted:
								Log.Verbose("Live subscription {subscriptionId} to {streamName} stream deleted.",
									_subscriptionId, _streamName);
								_readHistoricalEventsCompleted.TrySetException(RpcExceptions.StreamDeleted(streamName));
								return;
							case ReadStreamResult.AccessDenied:
								_readHistoricalEventsCompleted.TrySetException(RpcExceptions.AccessDenied());
								return;
							default:
								_readHistoricalEventsCompleted.TrySetException(
									RpcExceptions.UnknownError(completed.Result));
								return;
						}
					}

					void ReadHistoricalEvents(StreamRevision fromStreamRevision) {
						if (fromStreamRevision == StreamRevision.End) {
							throw new ArgumentOutOfRangeException(nameof(fromStreamRevision));
						}

						Log.Verbose(
							"Live subscription {subscriptionId} to {streamName} loading any missed events starting from {streamRevision}",
							subscriptionId, streamName, fromStreamRevision);

						var correlationId = Guid.NewGuid();
						bus.Publish(new ClientMessage.ReadStreamEventsForward(correlationId, correlationId,
							new CallbackEnvelope(OnHistoricalEventsMessage), streamName, fromStreamRevision.ToInt64(),
							ReadBatchSize, resolveLinks, _requiresLeader, null, user));
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

						var streamRevision = StreamRevision.FromInt64(historicalEvent.OriginalEventNumber);

						_current = historicalEvent;
						_currentStreamRevision = streamRevision;
						Log.Verbose(
							"Live subscription {subscriptionId} to {streamName} received event {streamRevision} historically.",
							_subscriptionId, _streamName, streamRevision);
						return true;
					}

					var delay = 1;

					while (!_liveEventBuffer.TryDequeue(out _)) {
						if (MaxBufferSizeExceeded) {
							return false;
						}

						delay = Math.Min(delay * 2, 50);
						await Task.Delay(delay, _disposedTokenSource.Token)
							.ConfigureAwait(false);
					}

					var (resolvedEvent, exception) = _;

					if (exception != null) {
						throw exception;
					}

					_currentStreamRevision = StreamRevision.FromInt64(resolvedEvent.OriginalEventNumber);
					Log.Verbose(
						"Live subscription {subscriptionId} to {streamName} received event {streamRevision} live.",
						_subscriptionId, _streamName, _currentStreamRevision);
					_current = resolvedEvent;
					return true;
				}

				private void MaximumBufferSizeExceeded() {
					Interlocked.Exchange(ref _maxBufferSizeExceeded, 1);
					Log.Warning("Live subscription {subscriptionId} to {streamName} buffer is full.",
						_subscriptionId, _streamName);

					_liveEventBuffer.Clear();
					_historicalEventBuffer.Clear();
				}

				public ValueTask DisposeAsync() {
					Log.Information("Live subscription {subscriptionId} to {streamName} disposed.", _subscriptionId,
						_streamName);
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
