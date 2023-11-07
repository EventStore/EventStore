using System;
using System.Collections.Generic;
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
	partial class Enumerator {
		public class AllSubscriptionFiltered : IAsyncEnumerator<ReadResponse> {
			private static readonly ILogger Log = Serilog.Log.ForContext<AllSubscriptionFiltered>();

			private readonly IExpiryStrategy _expiryStrategy;
			private readonly Guid _subscriptionId;
			private readonly IPublisher _bus;
			private readonly bool _resolveLinks;
			private readonly IEventFilter _eventFilter;
			private readonly ClaimsPrincipal _user;
			private readonly bool _requiresLeader;
			private readonly IReadIndex _readIndex;
			private readonly uint _maxSearchWindow;
			private readonly CancellationToken _cancellationToken;
			private readonly Channel<ReadResponse> _channel;
			private readonly uint _checkpointInterval;
			private readonly SemaphoreSlim _semaphore;

			private ReadResponse _current;
			private bool _disposed;
			private long _checkpointIntervalCounter;
			private int _subscriptionStarted;
			private Position? _currentPosition;

			public ReadResponse Current => _current;
			public string SubscriptionId { get; }

			public AllSubscriptionFiltered(IPublisher bus,
				IExpiryStrategy expiryStrategy,
				Position? startPosition,
				bool resolveLinks,
				IEventFilter eventFilter,
				ClaimsPrincipal user,
				bool requiresLeader,
				IReadIndex readIndex,
				uint? maxSearchWindow,
				uint checkpointIntervalMultiplier,
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

				if (checkpointIntervalMultiplier == 0) {
					throw new ArgumentOutOfRangeException(nameof(checkpointIntervalMultiplier));
				}

				_expiryStrategy = expiryStrategy;
				_subscriptionId = Guid.NewGuid();
				_bus = bus;
				_resolveLinks = resolveLinks;
				_eventFilter = eventFilter;
				_user = user;
				_requiresLeader = requiresLeader;
				_readIndex = readIndex;
				_maxSearchWindow = maxSearchWindow ?? ReadBatchSize;
				_cancellationToken = cancellationToken;
				_subscriptionStarted = 0;
				_channel = Channel.CreateBounded<ReadResponse>(BoundedChannelOptions);
				_checkpointInterval = checkpointIntervalMultiplier * _maxSearchWindow;
				_semaphore = new SemaphoreSlim(1, 1);

				SubscriptionId = _subscriptionId.ToString();

				_currentPosition = null;

				Subscribe(startPosition);
			}

			public ValueTask DisposeAsync() {
				if (_disposed) {
					return new ValueTask(Task.CompletedTask);
				}

				Log.Information("Live subscription {subscriptionId} to $all:{eventFilter} disposed.", _subscriptionId,
					_eventFilter);

				_disposed = true;
				_channel.Writer.TryComplete();
				Unsubscribe();
				return new ValueTask(Task.CompletedTask);
			}

			public async ValueTask<bool> MoveNextAsync() {
				ReadLoop:

				if (!await _channel.Reader.WaitToReadAsync(_cancellationToken).ConfigureAwait(false)) {
					return false;
				}

				var readResponse = await _channel.Reader.ReadAsync(_cancellationToken).ConfigureAwait(false);

				if (readResponse is ReadResponse.EventReceived eventReceived) {
					var eventPos = eventReceived.Event.OriginalPosition!.Value;
					var position = Position.FromInt64(eventPos.CommitPosition, eventPos.PreparePosition);

					if (_currentPosition.HasValue && position <= _currentPosition.Value) {
						Log.Verbose(
							"Subscription {subscriptionId} to $all:{eventFilter} skipping event {position}.",
							_subscriptionId, _eventFilter, position);
						goto ReadLoop;
					}

					_currentPosition = position;

					Log.Verbose(
						"Subscription {subscriptionId} to $all:{eventFilter} seen event {position}.",
						_subscriptionId, _eventFilter, position);
				}

				_current = readResponse;

				return true;
			}

			private void Subscribe(Position? startPosition) {
				if (startPosition == Position.End) {
					GoLive(Position.End);
				}
				else if (startPosition == null || startPosition == Position.Start) {
					CatchUp(Position.Start);
				} else {
					var (commitPosition, preparePosition) = startPosition.Value.ToInt64();
					try {
						var indexResult =
							_readIndex.ReadAllEventsForward(new TFPos(commitPosition, preparePosition), 1);
						CatchUp(Position.FromInt64(indexResult.NextPos.CommitPosition,
							indexResult.NextPos.PreparePosition));
					} catch (Exception ex) {
						Log.Error(ex, "Error starting catch-up subscription {subscriptionId} to $all:{eventFilter}@{position}",
							_subscriptionId, _eventFilter, startPosition);
						throw;
					}
				}
			}

			private void CatchUp(Position startPosition) {
				Log.Information(
					"Catch-up subscription {subscriptionId} to $all:{eventFilter}@{position} running...",
					_subscriptionId, _eventFilter, startPosition);

				ReadPage(startPosition, OnMessage);

				async Task OnMessage(Message message, CancellationToken ct) {
					if (message is ClientMessage.NotHandled notHandled &&
					    TryHandleNotHandled(notHandled, out var ex)) {
						Fail(ex);
						return;
					}

					if (message is not ClientMessage.FilteredReadAllEventsForwardCompleted completed) {
						Fail(ReadResponseException.UnknownMessage.Create<ClientMessage.FilteredReadAllEventsForwardCompleted>(message));
						return;
					}

					switch (completed.Result) {
						case FilteredReadAllResult.Success:
							await ConfirmSubscription().ConfigureAwait(false);

							var position = Position.FromInt64(completed.CurrentPos.CommitPosition,
								completed.CurrentPos.PreparePosition);
							foreach (var @event in completed.Events) {
								position = Position.FromInt64(
									@event.OriginalPosition!.Value.CommitPosition,
									@event.OriginalPosition!.Value.PreparePosition);

								Log.Verbose(
									"Catch-up subscription {subscriptionId} to $all:{eventFilter} received event {position}.",
									_subscriptionId, _eventFilter, position);

								await _channel.Writer.WriteAsync(new ReadResponse.EventReceived(@event), ct).ConfigureAwait(false);
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

								await _channel.Writer.WriteAsync(new ReadResponse.CheckpointReceived(
										commitPosition: position.CommitPosition,
										preparePosition: position.PreparePosition), ct)
									.ConfigureAwait(false);
							}

							ReadPage(nextPosition, OnMessage);
							return;

						case FilteredReadAllResult.Expired:
							ReadPage(
								Position.FromInt64(
									completed.CurrentPos.CommitPosition,
									completed.CurrentPos.PreparePosition),
								OnMessage);
							return;

						case FilteredReadAllResult.AccessDenied:
							Fail(new ReadResponseException.AccessDenied());
							return;
						default:
							Fail(ReadResponseException.UnknownError.Create(completed.Result));
							return;
					}
				}
			}

			private void GoLive(Position startPosition) {
				var liveEvents = Channel.CreateBounded<Message>(BoundedChannelOptions);
				var caughtUpSource = new TaskCompletionSource<Position>();
				var liveMessagesCancelled = 0;

				Log.Information(
					"Live subscription {subscriptionId} to $all running from {position}...",
					_subscriptionId, startPosition);

				_bus.Publish(new ClientMessage.FilteredSubscribeToStream(Guid.NewGuid(), _subscriptionId,
					new ContinuationEnvelope(OnSubscriptionMessage, _semaphore, _cancellationToken), _subscriptionId,
					string.Empty, _resolveLinks, _user, _eventFilter, (int)_checkpointInterval, (int)_checkpointIntervalCounter));

				Task.Factory.StartNew(PumpLiveMessages, _cancellationToken);

				async Task PumpLiveMessages() {
					await caughtUpSource.Task.ConfigureAwait(false);

					await _channel.Writer.WriteAsync(new ReadResponse.SubscriptionCaughtUp(), _cancellationToken).ConfigureAwait(false);

					await foreach (var message in liveEvents.Reader.ReadAllAsync(_cancellationToken)
						.ConfigureAwait(false)) {

						if (message is ClientMessage.CheckpointReached checkpoint) {
							if (checkpoint.Position == null) {
								_channel.Writer.Complete(
									new Exception("Unexpected error, checkpoint position is Null"));
								break;
							}

							var checkpointPosition = Position.FromInt64(
								checkpoint.Position.Value.CommitPosition,
								checkpoint.Position.Value.PreparePosition);

							await _channel.Writer
								.WriteAsync(
									new ReadResponse.CheckpointReceived(
										commitPosition: checkpointPosition.CommitPosition,
										preparePosition: checkpointPosition.PreparePosition), _cancellationToken)
								.ConfigureAwait(false);
						} else if (message is ClientMessage.StreamEventAppeared evt) {
							await _channel.Writer.WriteAsync(new ReadResponse.EventReceived(evt.Event), _cancellationToken)
								.ConfigureAwait(false);
						}
					}
				}

				async Task OnSubscriptionMessage(Message message, CancellationToken ct) {
					if (message is ClientMessage.NotHandled notHandled &&
					    TryHandleNotHandled(notHandled, out var ex)) {
						Fail(ex);
						return;
					}

					switch (message) {
						case ClientMessage.SubscriptionConfirmation confirmed:
							await ConfirmSubscription().ConfigureAwait(false);

							var caughtUp = new TFPos(confirmed.LastIndexedPosition, confirmed.LastIndexedPosition);
							Log.Verbose(
								"Live subscription {subscriptionId} to $all:{eventFilter} confirmed at {position}.",
								_subscriptionId, _eventFilter, caughtUp);

							if (startPosition != Position.End) {
								ReadHistoricalEvents(startPosition);
							} else {
								NotifyCaughtUp(Position.FromInt64(caughtUp.CommitPosition, caughtUp.PreparePosition));
							}

							void NotifyCaughtUp(Position position) {
								Log.Verbose(
									"Live subscription {subscriptionId} to $all:{eventFilter} caught up at {streamRevision} because the end of stream was reached.",
									_subscriptionId, _eventFilter, position);
								caughtUpSource.TrySetResult(position);
							}

#pragma warning disable CS1998 // Async method lacks 'await' operators and will run synchronously
							async Task OnHistoricalEventsMessage(Message message, CancellationToken ct) {
#pragma warning restore CS1998
								if (message is ClientMessage.NotHandled notHandled &&
								    TryHandleNotHandled(notHandled, out var ex)) {
									Fail(ex);
									return;
								}

								if (message is not ClientMessage.FilteredReadAllEventsForwardCompleted completed) {
									Fail(ReadResponseException.UnknownMessage.Create<ClientMessage.FilteredReadAllEventsForwardCompleted>(message));
									return;
								}

								switch (completed.Result) {
									case FilteredReadAllResult.Success:
										if (completed.Events.Length == 0 && completed.IsEndOfStream) {
											NotifyCaughtUp(Position.FromInt64(completed.CurrentPos.CommitPosition,
												completed.CurrentPos.PreparePosition));
											return;
										}

										foreach (var @event in completed.Events) {
											var position = @event.OriginalPosition!.Value;

											if (position > caughtUp) {
												NotifyCaughtUp(Position.FromInt64(position.CommitPosition,
													position.PreparePosition));
												return;
											}

											Log.Verbose(
												"Live subscription {subscriptionId} to $all:{eventFilter} enqueuing historical message {position}.",
												_subscriptionId, _eventFilter, position);
											if (!_channel.Writer.TryWrite(new ReadResponse.EventReceived(@event))) {
												ConsumerTooSlow(@event);
												return;
											}
										}

										ReadHistoricalEvents(Position.FromInt64(
											completed.NextPos.CommitPosition,
											completed.NextPos.PreparePosition));
										return;

										void NotifyCaughtUp(Position position) {
											Log.Verbose(
												"Live subscription {subscriptionId} to $all:{eventFilter} caught up at {position} because the end of stream was reached.",
												_subscriptionId, _eventFilter, position);

											caughtUpSource.TrySetResult(position);
										}

									case FilteredReadAllResult.Expired:
										ReadHistoricalEvents(Position.FromInt64(
											completed.CurrentPos.CommitPosition,
											completed.CurrentPos.PreparePosition));
										return;

									case FilteredReadAllResult.AccessDenied:
										Fail(new ReadResponseException.AccessDenied());
										return;
									default:
										Fail(ReadResponseException.UnknownError.Create(completed.Result));
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
									Fail(new ReadResponseException.AccessDenied());
									return;
								case SubscriptionDropReason.Unsubscribed:
									return;
								default:
									Fail(ReadResponseException.UnknownError.Create(dropped.Reason));
									return;
							}
						case ClientMessage.StreamEventAppeared appeared: {
							if (liveMessagesCancelled == 1) {
								return;
							}

							Log.Verbose(
								"Live subscription {subscriptionId} to $all:{eventFilter} enqueuing live message {position}.",
								_subscriptionId, _eventFilter, appeared.Event.OriginalPosition);

							if (!liveEvents.Writer.TryWrite(appeared)) {
								ConsumerTooSlow(appeared.Event);
							}

							return;
						}
						case ClientMessage.CheckpointReached checkpointReached:
							if (!liveEvents.Writer.TryWrite(checkpointReached)) {
								ConsumerTooSlow(null);
							}
							return;
						default:
							Fail(ReadResponseException.UnknownMessage.Create<ClientMessage.SubscriptionConfirmation>(message));
							return;
					}
				}

				void ConsumerTooSlow(ResolvedEvent? evt) {
					if (Interlocked.Exchange(ref liveMessagesCancelled, 1) != 0)
						return;

					var state = caughtUpSource.Task.Status == TaskStatus.RanToCompletion ? "live" : "transitioning to live";
					var msg = $"Consumer too slow to handle event while {state}. Client resubscription required.";
					Log.Information(
						"Live subscription {subscriptionId} to $all:{eventFilter} timed out at {position}. {msg}. unsubscribing...",
						_subscriptionId, _eventFilter, evt?.OriginalPosition.GetValueOrDefault(), msg);

					Unsubscribe();

					liveEvents.Writer.Complete();

					Fail(new ReadResponseException.Timeout(msg));
				}

				void Fail(Exception exception) {
					this.Fail(exception);
					caughtUpSource.TrySetException(exception);
				}
			}

			private ValueTask ConfirmSubscription() => Interlocked.CompareExchange(ref _subscriptionStarted, 1, 0) != 0
				? new ValueTask(Task.CompletedTask)
				: _channel.Writer.WriteAsync(new ReadResponse.SubscriptionConfirmed(SubscriptionId), _cancellationToken);

			private void Fail(Exception exception) {
				Interlocked.Exchange(ref _subscriptionStarted, 1);
				_channel.Writer.TryComplete(exception);
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
					(int)_maxSearchWindow, null, _eventFilter, _user,
					replyOnExpired: true,
					expires: _expiryStrategy.GetExpiry(),
					cancellationToken: _cancellationToken));
			}

			private void Unsubscribe() => _bus.Publish(new ClientMessage.UnsubscribeFromStream(Guid.NewGuid(),
				_subscriptionId, new NoopEnvelope(), _user));
		}
	}
}
