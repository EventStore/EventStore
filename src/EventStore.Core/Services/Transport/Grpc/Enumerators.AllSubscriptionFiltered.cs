using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Storage.ReaderIndex;
using Serilog;
using IReadIndex = EventStore.Core.Services.Storage.ReaderIndex.IReadIndex;

namespace EventStore.Core.Services.Transport.Grpc {
	public static partial class Enumerators {
		public class AllSubscriptionFiltered : ISubscriptionEnumerator {
			private static readonly ILogger Log = Serilog.Log.ForContext<AllSubscriptionFiltered>();

			private readonly Guid _subscriptionId;
			private readonly IPublisher _bus;
			private readonly bool _resolveLinks;
			private readonly IEventFilter _eventFilter;
			private readonly ClaimsPrincipal _user;
			private readonly bool _requiresLeader;
			private readonly uint _maxSearchWindow;
			private readonly Func<Position, Task> _checkpointReached;
			private readonly TaskCompletionSource<bool> _subscriptionStarted;
			private readonly CancellationToken _cancellationToken;
			private readonly Channel<(ResolvedEvent?, Position?)> _channel;
			private readonly uint _checkpointInterval;
			private readonly SemaphoreSlim _semaphore;
			private readonly TFPos _startPositionExclusive;

			private ResolvedEvent? _current;
			private bool _disposed;
			private long _checkpointIntervalCounter;
			private Position _lastCheckpoint;

			public ResolvedEvent Current => _current.GetValueOrDefault();
			public Task Started => _subscriptionStarted.Task;
			public string SubscriptionId { get; }

			public AllSubscriptionFiltered(IPublisher bus,
				Position? startPosition,
				bool resolveLinks,
				IEventFilter eventFilter,
				ClaimsPrincipal user,
				bool requiresLeader,
				IReadIndex readIndex,
				uint? maxSearchWindow,
				uint checkpointIntervalMultiplier,
				Func<Position, Task> checkpointReached,
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

				if (checkpointReached == null) {
					throw new ArgumentNullException(nameof(checkpointReached));
				}

				if (checkpointIntervalMultiplier == 0) {
					throw new ArgumentOutOfRangeException(nameof(checkpointIntervalMultiplier));
				}

				_subscriptionId = Guid.NewGuid();
				_bus = bus;
				_resolveLinks = resolveLinks;
				_eventFilter = eventFilter;
				_user = user;
				_requiresLeader = requiresLeader;
				_maxSearchWindow = maxSearchWindow ?? ReadBatchSize;
				_checkpointReached = checkpointReached;
				_cancellationToken = cancellationToken;
				_subscriptionStarted = new TaskCompletionSource<bool>();
				_channel = Channel.CreateBounded<(ResolvedEvent?, Position?)>(BoundedChannelOptions);
				_checkpointInterval = checkpointIntervalMultiplier * _maxSearchWindow;
				_semaphore = new SemaphoreSlim(1, 1);
				_lastCheckpoint = Position.Start;

				SubscriptionId = _subscriptionId.ToString();

				var lastIndexedPosition = readIndex.LastIndexedPosition;
				var startPositionExclusive = startPosition == Position.End
					? Position.FromInt64(lastIndexedPosition, lastIndexedPosition)
					: startPosition ?? Position.Start;

				_startPositionExclusive = new TFPos((long)startPositionExclusive.CommitPosition,
					(long)startPositionExclusive.PreparePosition);

				Subscribe(startPositionExclusive, startPosition != Position.End);
			}

			public ValueTask DisposeAsync() {
				if (_disposed) {
					return new ValueTask(Task.CompletedTask);
				}

				Log.Information("Live subscription {subscriptionId} to $all:{eventFilter} disposed.", _subscriptionId,
					_eventFilter);

				_disposed = true;
				_semaphore.Dispose();
				_channel.Writer.TryComplete();
				Unsubscribe();
				return new ValueTask(Task.CompletedTask);
			}

			public async ValueTask<bool> MoveNextAsync() {
				ReadLoop:
				if (!await _channel.Reader.WaitToReadAsync(_cancellationToken).ConfigureAwait(false)) {
					return false;
				}

				var (@event, position) = await _channel.Reader.ReadAsync(_cancellationToken).ConfigureAwait(false);
				if (@event.HasValue) {
					if (@event.Value.OriginalPosition.Value <= _startPositionExclusive || _current.HasValue &&
					    @event.Value.OriginalPosition.Value <= _current.Value.OriginalPosition.Value) {
						Log.Verbose(
							"Subscription {subscriptionId} to $all:{eventFilter} skipping event {position}.",
							_subscriptionId, _eventFilter, @event.Value.OriginalPosition.Value);
						goto ReadLoop;
					}

					Log.Verbose(
						"Subscription {subscriptionId} to $all:{eventFilter} seen event {position}.",
						_subscriptionId, _eventFilter, @event.Value.OriginalPosition.Value);

					_current = @event.Value;
					return true;
				}

				if (position.HasValue && position.Value > _lastCheckpoint) {
					await _checkpointReached(position.Value).ConfigureAwait(false);
					_lastCheckpoint = position.Value;
				}

				goto ReadLoop;
			}

			private void Subscribe(Position startPosition, bool catchUp) {
				if (catchUp) {
					CatchUp(startPosition);
				} else {
					GoLive(startPosition);
				}
			}

			private void CatchUp(Position startPosition) {
				Log.Information(
					"Catch-up subscription {subscriptionId} to $all:{eventFilter}@{position} running...",
					_subscriptionId, _eventFilter, startPosition);

				ReadPage(startPosition, OnMessage);

				async Task OnMessage(Message message, CancellationToken ct) {
					if (message is ClientMessage.NotHandled notHandled &&
					    RpcExceptions.TryHandleNotHandled(notHandled, out var ex)) {
						Fail(ex);
						return;
					}

					if (!(message is ClientMessage.FilteredReadAllEventsForwardCompleted completed)) {
						Fail(RpcExceptions.UnknownMessage<ClientMessage.FilteredReadAllEventsForwardCompleted>(
							message));
						return;
					}

					switch (completed.Result) {
						case FilteredReadAllResult.Success:
							ConfirmSubscription();

							var position = Position.FromInt64(completed.CurrentPos.CommitPosition,
								completed.CurrentPos.PreparePosition);
							foreach (var @event in completed.Events) {
								position = Position.FromInt64(
									@event.OriginalPosition.Value.CommitPosition,
									@event.OriginalPosition.Value.PreparePosition);

								Log.Verbose(
									"Catch-up subscription {subscriptionId} to $all:{eventFilter} received event {position}.",
									_subscriptionId, _eventFilter, position);

								await _channel.Writer.WriteAsync((@event, new Position?()), ct).ConfigureAwait(false);
							}

							_checkpointIntervalCounter += completed.ConsideredEventsCount;
							Log.Verbose(
								"Catch-up subscription {subscriptionId} to $all:{eventFilter} considered {consideredEventsCount}, interval: {checkpointInterval}, counter: {checkpointIntervalCounter}.",
								_subscriptionId, _eventFilter, completed.ConsideredEventsCount, _checkpointInterval,
								_checkpointIntervalCounter);

							var nextPosition = Position.FromInt64(completed.NextPos.CommitPosition,
								completed.NextPos.PreparePosition);

							if (completed.IsEndOfStream) {
								GoLive(nextPosition);
								return;
							}

							if (_checkpointIntervalCounter >= _checkpointInterval) {
								_checkpointIntervalCounter %= _checkpointInterval;
								Log.Verbose(
									"Catch-up subscription {subscriptionId} to $all:{eventFilter} reached checkpoint at {position}, interval: {checkpointInterval}, counter: {checkpointIntervalCounter}.",
									_subscriptionId, _eventFilter, nextPosition, _checkpointInterval,
									_checkpointIntervalCounter);

								await _channel.Writer.WriteAsync((new ResolvedEvent?(), position), ct)
									.ConfigureAwait(false);
							}

							ReadPage(nextPosition, OnMessage);

							return;
						case FilteredReadAllResult.AccessDenied:
							Fail(RpcExceptions.AccessDenied());
							return;
						default:
							Fail(RpcExceptions.UnknownError(completed.Result));
							return;
					}
				}
			}

			private void GoLive(Position startPosition) {
				var liveEvents = Channel.CreateBounded<ResolvedEvent>(BoundedChannelOptions);
				var caughtUpSource = new TaskCompletionSource<TFPos>();
				var liveMessagesCancelled = 0;

				Log.Information(
					"Live subscription {subscriptionId} to $all running from {position}...",
					_subscriptionId, startPosition);

				_bus.Publish(new ClientMessage.FilteredSubscribeToStream(Guid.NewGuid(), _subscriptionId,
					new ContinuationEnvelope(OnSubscriptionMessage, _semaphore, _cancellationToken), _subscriptionId,
					string.Empty, _resolveLinks, _user, _eventFilter, (int)_checkpointInterval));

				Task.Factory.StartNew(PumpLiveMessages, _cancellationToken);

				async Task PumpLiveMessages() {
					await caughtUpSource.Task.ConfigureAwait(false);
					await foreach (var @event in liveEvents.Reader.ReadAllAsync(_cancellationToken)
						.ConfigureAwait(false)) {
						await _channel.Writer.WriteAsync((@event, new Position?()), _cancellationToken)
							.ConfigureAwait(false);
					}
				}

				async Task OnSubscriptionMessage(Message message, CancellationToken ct) {
					if (message is ClientMessage.NotHandled notHandled &&
					    RpcExceptions.TryHandleNotHandled(notHandled, out var ex)) {
						Fail(ex);
						return;
					}

					switch (message) {
						case ClientMessage.SubscriptionConfirmation confirmed:
							ConfirmSubscription();

							var caughtUp = new TFPos(confirmed.LastIndexedPosition, confirmed.LastIndexedPosition);
							Log.Verbose(
								"Live subscription {subscriptionId} to $all:{eventFilter} confirmed at {position}.",
								_subscriptionId, _eventFilter, caughtUp);

							ReadHistoricalEvents(startPosition);

							async Task OnHistoricalEventsMessage(Message message, CancellationToken ct) {
								if (message is ClientMessage.NotHandled notHandled &&
								    RpcExceptions.TryHandleNotHandled(notHandled, out var ex)) {
									Fail(ex);
									return;
								}

								if (!(message is ClientMessage.FilteredReadAllEventsForwardCompleted completed)) {
									Fail(RpcExceptions
										.UnknownMessage<ClientMessage.FilteredReadAllEventsForwardCompleted>(
											message));
									return;
								}

								switch (completed.Result) {
									case FilteredReadAllResult.Success:
										if (completed.Events.Length == 0 && completed.IsEndOfStream) {
											await NotifyCaughtUp(completed.CurrentPos).ConfigureAwait(false);
											return;
										}

										foreach (var @event in completed.Events) {
											var position = @event.OriginalPosition.Value;

											if (position > caughtUp) {
												await NotifyCaughtUp(position).ConfigureAwait(false);
												return;
											}

											Log.Verbose(
												"Live subscription {subscriptionId} to $all:{eventFilter} enqueuing historical message {position}.",
												_subscriptionId, _eventFilter, position);
											await _channel.Writer.WriteAsync((@event, new Position?()),
												_cancellationToken).ConfigureAwait(false);
										}

										ReadHistoricalEvents(Position.FromInt64(
											completed.NextPos.CommitPosition,
											completed.NextPos.PreparePosition));
										return;

										async Task NotifyCaughtUp(TFPos position) {
											Log.Verbose(
												"Live subscription {subscriptionId} to $all:{eventFilter} caught up at {position} because the end of stream was reached.",
												_subscriptionId, _eventFilter, position);
											await _channel.Writer.WriteAsync(
												(new ResolvedEvent?(),
													Position.FromInt64(position.CommitPosition,
														position.PreparePosition)
												), ct).ConfigureAwait(false);
											caughtUpSource.TrySetResult(caughtUp);
										}
									case FilteredReadAllResult.AccessDenied:
										Fail(RpcExceptions.AccessDenied());
										return;
									default:
										Fail(RpcExceptions.UnknownError(completed.Result));
										return;
								}
							}

							void ReadHistoricalEvents(Position fromPosition) {
								if (fromPosition == Position.End) {
									throw new ArgumentOutOfRangeException(nameof(fromPosition));
								}

								Log.Verbose(
									"Live subscription {subscriptionId} to $all:{eventFilter} loading any missed events ending with {position}.",
									_subscriptionId, _eventFilter, fromPosition);

								ReadPage(fromPosition, OnHistoricalEventsMessage);
							}

							return;
						case ClientMessage.SubscriptionDropped dropped:
							switch (dropped.Reason) {
								case SubscriptionDropReason.AccessDenied:
									Fail(RpcExceptions.AccessDenied());
									return;
								case SubscriptionDropReason.Unsubscribed:
									return;
								default:
									Fail(RpcExceptions.UnknownError(dropped.Reason));
									return;
							}
						case ClientMessage.StreamEventAppeared appeared: {
							if (liveMessagesCancelled == 1) {
								return;
							}

							using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
							try {
								Log.Verbose(
									"Live subscription {subscriptionId} to $all:{eventFilter} enqueuing live message {position}.",
									_subscriptionId, _eventFilter, appeared.Event.OriginalPosition);

								await liveEvents.Writer.WriteAsync(appeared.Event, cts.Token)
									.ConfigureAwait(false);
							} catch (Exception e) {
								if (Interlocked.Exchange(ref liveMessagesCancelled, 1) != 0) return;

								Log.Verbose(
									e,
									"Live subscription {subscriptionId} to $all:{eventFilter} timed out at {position}; unsubscribing...",
									_subscriptionId, _eventFilter, appeared.Event.OriginalPosition.GetValueOrDefault());

								Unsubscribe();

								liveEvents.Writer.Complete();

								var originalPosition = _current.GetValueOrDefault()
									.OriginalPosition.GetValueOrDefault();
								CatchUp(Position.FromInt64(
									originalPosition.CommitPosition,
									originalPosition.PreparePosition));
							}

							return;
						}
						case ClientMessage.CheckpointReached checkpointReached:
							await _channel.Writer.WriteAsync((new ResolvedEvent?(), Position.FromInt64(
								checkpointReached.Position.Value.CommitPosition,
								checkpointReached.Position.Value.PreparePosition)), ct).ConfigureAwait(false);
							return;
						default:
							Fail(RpcExceptions.UnknownMessage<ClientMessage.SubscriptionConfirmation>(message));
							return;
					}
				}

				void Fail(Exception exception) {
					this.Fail(exception);
					caughtUpSource.TrySetException(exception);
				}
			}

			private void ConfirmSubscription() {
				if (_subscriptionStarted.Task.IsCompletedSuccessfully) return;
				_subscriptionStarted.TrySetResult(true);
			}

			private void Fail(Exception exception) {
				_channel.Writer.TryComplete(exception);
				_subscriptionStarted.TrySetException(exception);
			}

			private void ReadPage(Position position, Func<Message, CancellationToken, Task> onMessage) {
				Guid correlationId = Guid.NewGuid();
				Log.Verbose(
					"Subscription {subscriptionId} to $all:{eventFilter} reading next page starting from {nextRevision}.",
					_subscriptionId, _eventFilter, position);

				var (commitPosition, preparePosition) = position.ToInt64();

				_bus.Publish(new ClientMessage.FilteredReadAllEventsForward(
					correlationId, correlationId, new ContinuationEnvelope(onMessage, _semaphore, _cancellationToken),
					commitPosition, preparePosition, ReadBatchSize, _resolveLinks, _requiresLeader,
					(int)_maxSearchWindow, default, _eventFilter, _user));
			}

			private void Unsubscribe() => _bus.Publish(new ClientMessage.UnsubscribeFromStream(Guid.NewGuid(),
				_subscriptionId, new NoopEnvelope(), _user));
		}
	}
}
