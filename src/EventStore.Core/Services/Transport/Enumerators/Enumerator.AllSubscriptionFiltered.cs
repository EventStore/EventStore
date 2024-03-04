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
using EventStore.Core.Services.Transport.Common;
using Serilog;

namespace EventStore.Core.Services.Transport.Enumerators {
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
			private readonly uint _maxSearchWindow;
			private readonly uint _checkpointInterval;
			private readonly CancellationTokenSource _cts;
			private readonly Channel<ReadResponse> _channel;
			private readonly Channel<(ulong SequenceNumber, ResolvedEvent? ResolvedEvent, TFPos? Checkpoint)> _liveEvents;

			private ReadResponse _current;
			private bool _disposed;
			private Position? _currentPosition;
			private Position? _currentCheckpoint;

			public ReadResponse Current => _current;
			public string SubscriptionId { get; }

			public AllSubscriptionFiltered(IPublisher bus,
				IExpiryStrategy expiryStrategy,
				Position? checkpoint,
				bool resolveLinks,
				IEventFilter eventFilter,
				ClaimsPrincipal user,
				bool requiresLeader,
				uint? maxSearchWindow,
				uint checkpointIntervalMultiplier,
				CancellationToken cancellationToken) {
				ArgumentNullException.ThrowIfNull(bus);
				ArgumentNullException.ThrowIfNull(eventFilter);
				ArgumentOutOfRangeException.ThrowIfZero(checkpointIntervalMultiplier);

				_expiryStrategy = expiryStrategy;
				_subscriptionId = Guid.NewGuid();
				_bus = bus;
				_resolveLinks = resolveLinks;
				_eventFilter = eventFilter;
				_user = user;
				_requiresLeader = requiresLeader;
				_maxSearchWindow = maxSearchWindow ?? ReadBatchSize;
				_checkpointInterval = checkpointIntervalMultiplier * _maxSearchWindow;
				_cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
				_channel = Channel.CreateBounded<ReadResponse>(BoundedChannelOptions);
				_liveEvents = Channel.CreateBounded<(ulong, ResolvedEvent?, TFPos?)>(LiveChannelOptions);

				SubscriptionId = _subscriptionId.ToString();

				Subscribe(checkpoint, _cts.Token);
			}

			public ValueTask DisposeAsync() {
				if (_disposed) {
					return ValueTask.CompletedTask;
				}

				Log.Debug("Subscription {subscriptionId} to $all:{eventFilter} disposed.", _subscriptionId, _eventFilter);

				_disposed = true;
				Unsubscribe();

				_cts.Cancel();
				_cts.Dispose();

				return ValueTask.CompletedTask;
			}

			public async ValueTask<bool> MoveNextAsync() {
				ReadLoop:

				if (!await _channel.Reader.WaitToReadAsync(_cts.Token)) {
					return false;
				}

				var readResponse = await _channel.Reader.ReadAsync(_cts.Token);

				if (readResponse is ReadResponse.EventReceived eventReceived) {
					var eventPos = eventReceived.Event.OriginalPosition!.Value;
					var position = Position.FromInt64(eventPos.CommitPosition, eventPos.PreparePosition);

					if (_currentPosition.HasValue && position <= _currentPosition.Value) {
						// this should no longer happen
						Log.Warning("Subscription {subscriptionId} to $all:{eventFilter} skipping event {position} as it is less than {currentPosition}.",
							_subscriptionId, _eventFilter, position, _currentPosition);
						goto ReadLoop;
					}

					Log.Verbose(
						"Subscription {subscriptionId} to $all:{eventFilter} seen event {position}.",
						_subscriptionId, _eventFilter, position);
					
					_currentPosition = position;
				} else if (readResponse is ReadResponse.CheckpointReceived checkpointReceived) {
					var checkpointPos = new Position(checkpointReceived.CommitPosition, checkpointReceived.PreparePosition);

					if (_currentCheckpoint.HasValue && checkpointPos <= _currentCheckpoint.Value) {
						// in some cases, it's possible to receive the same checkpoint twice for example:
						// i) when the subscription goes live and it turns out that the next thing on the live channel is a checkpoint
						// ii) when catching-up, reaching the checkpoint interval and the next page read reaches the end of the stream

						goto ReadLoop;
					}

					Log.Verbose(
						"Subscription {subscriptionId} to $all:{eventFilter} received checkpoint {position}.",
						_subscriptionId, _eventFilter, checkpointPos);

					_currentCheckpoint = checkpointPos;
				}

				_current = readResponse;

				return true;
			}

			private void Subscribe(Position? checkpoint, CancellationToken ct) {
				Task.Factory.StartNew(() => MainLoop(checkpoint, ct), ct);
			}

			private static TFPos ConvertCheckpoint(Position? checkpoint, TFPos lastLivePos) {
				if (!checkpoint.HasValue)
					return new TFPos(-1, -1);

				if (checkpoint == Position.End)
					return lastLivePos;

				var (commitPos, preparePos) = checkpoint.Value.ToInt64();
				return new TFPos(commitPos, preparePos);
			}

			private async Task MainLoop(Position? checkpointPosition, CancellationToken ct) {
				try {
					Log.Information("Subscription {subscriptionId} to $all:{eventFilter} has started at checkpoint {position}",
						_subscriptionId, _eventFilter, checkpointPosition?.ToString() ?? "Start");

					var confirmationLastPos = await SubscribeToLive();
					await ConfirmSubscription(ct);

					// the event or checkpoint position we most recently sent on towards the client.
					// (we should send on events _after_ this and checkpoints _equal to or after_ this)
					var checkpoint = ConvertCheckpoint(checkpointPosition, confirmationLastPos);

					// the most recently read sequence number from the live channel. 0 when we haven't read any.
					var sequenceNumber = 0UL;

					if (checkpoint >= confirmationLastPos)
						(checkpoint, sequenceNumber) = await GoLive(checkpoint, sequenceNumber, ct);

					while (true) {
						ct.ThrowIfCancellationRequested();
						checkpoint = await CatchUp(checkpoint, ct);
						(checkpoint, sequenceNumber) = await GoLive(checkpoint, sequenceNumber, ct);
					}
				} catch (Exception ex) {
					if (ex is not (OperationCanceledException or ReadResponseException.InvalidPosition))
						Log.Error(ex, "Subscription {subscriptionId} to $all:{eventFilter} experienced an error.",
							_subscriptionId, _eventFilter);
					_channel.Writer.TryComplete(ex);
				} finally {
					Log.Information("Subscription {subscriptionId} to $all:{eventFilter} has ended.",
						_subscriptionId, _eventFilter);
				}
			}

			private async Task NotifyCaughtUp(TFPos checkpoint, CancellationToken ct) {
				Log.Debug(
					"Subscription {subscriptionId} to $all:{eventFilter} caught up at checkpoint {position}.",
					_subscriptionId, _eventFilter, checkpoint);

				await _channel.Writer.WriteAsync(new ReadResponse.SubscriptionCaughtUp(), ct);
			}

			private async Task NotifyFellBehind(TFPos checkpoint, CancellationToken ct) {
				Log.Debug(
					"Subscription {subscriptionId} to $all:{eventFilter} fell behind at checkpoint {position}.",
					_subscriptionId, _eventFilter, checkpoint);

				await _channel.Writer.WriteAsync(new ReadResponse.SubscriptionFellBehind(), ct);
			}

			private async ValueTask<(TFPos, ulong)> GoLive(TFPos checkpoint, ulong sequenceNumber, CancellationToken ct) {
				await NotifyCaughtUp(checkpoint, ct);

				await foreach (var liveEvent in _liveEvents.Reader.ReadAllAsync(ct)) {
					var sequenceCorrect = liveEvent.SequenceNumber == sequenceNumber + 1;
					sequenceNumber = liveEvent.SequenceNumber;

					if (liveEvent.ResolvedEvent.HasValue && liveEvent.ResolvedEvent.Value.OriginalPosition <= checkpoint) {
						// skip because this event has already been sent towards the client
						// (or the client specified a checkpoint for events that don't exist yet and so
						// is not interested in them)
						// nb: we can skip this event even if it had the wrong sequence number, it means
						// we would have skipped the events that got discarded anyway.
						continue;
					}

					if (liveEvent.Checkpoint.HasValue && liveEvent.Checkpoint.Value < checkpoint) {
						// skip because the checkpoint received is earlier than the last event or checkpoint sent towards the client
						continue;
					}

					if (!sequenceCorrect) {
						// there's a gap in the sequence numbers, at least one live event was discarded
						// due to the live channel becoming full.
						// switch back to catchup to make sure we didn't miss anything we wanted to send.
						await NotifyFellBehind(checkpoint, ct);

						// issue a checkpoint before falling behind to catch-up to make sure that
						// at least one checkpoint is issued within the checkpoint interval
						await SendCheckpointToSubscription(checkpoint, ct);

						return (checkpoint, sequenceNumber);
					}

					if (liveEvent.ResolvedEvent.HasValue) {
						// this is the next event to send towards the client. send it and update `checkpoint`
						await SendEventToSubscription(liveEvent.ResolvedEvent.Value, ct);
						checkpoint = liveEvent.ResolvedEvent.Value.OriginalPosition!.Value;
					} else if (liveEvent.Checkpoint.HasValue) {
						// this is a valid checkpoint that we can send towards the client. send it and update `checkpoint`
						await SendCheckpointToSubscription(liveEvent.Checkpoint.Value, ct);
						checkpoint = liveEvent.Checkpoint.Value;
					}
				}

				throw new Exception($"Unexpected error: live events channel for subscription {_subscriptionId} to $all:{_eventFilter} completed without exception");
			}

			private Task<TFPos> CatchUp(TFPos checkpoint, CancellationToken ct) {
				Log.Verbose(
					"Subscription {subscriptionId} to $all:{eventFilter} is catching up from checkpoint {position}",
					_subscriptionId, _eventFilter, checkpoint);

				var checkpointIntervalCounter = 0L;
				var catchupCompletionTcs = new TaskCompletionSource<TFPos>();

				// this is a safe use of AsyncTaskEnvelope. Only one call to OnMessage will be running
				// at any given time because we only expect one reply and that reply kicks off the next read.
				AsyncTaskEnvelope envelope = null;
				envelope = new AsyncTaskEnvelope(OnMessage, ct);

				ReadPage(checkpoint, envelope, ct);

				return catchupCompletionTcs.Task;

				async Task OnMessage(Message message, CancellationToken ct) {
					try {
						if (message is ClientMessage.NotHandled notHandled &&
						    TryHandleNotHandled(notHandled, out var ex))
							throw ex;

						if (message is not ClientMessage.FilteredReadAllEventsForwardCompleted completed)
							throw ReadResponseException.UnknownMessage
								.Create<ClientMessage.FilteredReadAllEventsForwardCompleted>(message);

						switch (completed.Result) {
							case FilteredReadAllResult.Success:
								foreach (var @event in completed.Events) {
									var eventPosition = @event.OriginalPosition!.Value;

									// this can only be true for the first event of the first page
									// as we start page reads from the checkpoint's position
									if (eventPosition <= checkpoint)
										continue;

									Log.Verbose(
										"Subscription {subscriptionId} to $all:{eventFilter} received catch-up event {position}.",
										_subscriptionId, _eventFilter, eventPosition);

									await SendEventToSubscription(@event, ct);
									checkpoint = eventPosition;
								}

								checkpointIntervalCounter += completed.ConsideredEventsCount;
								Log.Verbose(
									"Subscription {subscriptionId} to $all:{eventFilter} considered {consideredEventsCount} catch-up events (interval: {checkpointInterval}, counter: {checkpointIntervalCounter})",
									_subscriptionId, _eventFilter, completed.ConsideredEventsCount, _checkpointInterval, checkpointIntervalCounter);

								if (completed.IsEndOfStream) {
									// issue a checkpoint when going live to make sure that at least
									// one checkpoint is issued within the checkpoint interval
									await SendCheckpointToSubscription(checkpoint, ct);

									catchupCompletionTcs.TrySetResult(checkpoint);
									return;
								}

								if (checkpointIntervalCounter >= _checkpointInterval) {
									checkpointIntervalCounter %= _checkpointInterval;

									if (checkpoint < completed.CurrentPos)
										checkpoint = completed.CurrentPos;

									Log.Verbose(
										"Subscription {subscriptionId} to $all:{eventFilter} reached checkpoint at {position} during catch-up (interval: {checkpointInterval}, counter: {checkpointIntervalCounter})",
										_subscriptionId, _eventFilter, checkpoint, _checkpointInterval, checkpointIntervalCounter);

									await SendCheckpointToSubscription(checkpoint, ct);
								}

								ReadPage(completed.NextPos, envelope, ct);
								return;
							case FilteredReadAllResult.Expired:
								ReadPage(completed.CurrentPos, envelope, ct);
								return;
							case FilteredReadAllResult.AccessDenied:
								throw new ReadResponseException.AccessDenied();
							case FilteredReadAllResult.InvalidPosition:
								throw new ReadResponseException.InvalidPosition();
							default:
								throw ReadResponseException.UnknownError.Create(completed.Result);
						}
					} catch (Exception exception) {
						catchupCompletionTcs.TrySetException(exception);
					}
				}
			}

			private async Task SendEventToSubscription(ResolvedEvent @event, CancellationToken ct) {
				await _channel.Writer.WriteAsync(new ReadResponse.EventReceived(@event), ct);
			}

			private async Task SendCheckpointToSubscription(TFPos checkpoint, CancellationToken ct) {
				var checkpointPos = Position.FromInt64(checkpoint.CommitPosition, checkpoint.PreparePosition);
				await _channel.Writer.WriteAsync(new ReadResponse.CheckpointReceived(
					commitPosition: checkpointPos.CommitPosition,
					preparePosition: checkpointPos.PreparePosition), ct);
			}

			private Task<TFPos> SubscribeToLive() {
				var nextLiveSequenceNumber = 0UL;
				var confirmationPositionTcs = new TaskCompletionSource<TFPos>();

				_bus.Publish(new ClientMessage.FilteredSubscribeToStream(Guid.NewGuid(), _subscriptionId,
					new CallbackEnvelope(OnSubscriptionMessage), _subscriptionId,
					string.Empty, _resolveLinks, _user,
					_eventFilter, (int)_checkpointInterval, checkpointIntervalCurrent: 0));

				return confirmationPositionTcs.Task;

				void OnSubscriptionMessage(Message message) {
					try {
						if (message is ClientMessage.NotHandled notHandled &&
						    TryHandleNotHandled(notHandled, out var ex))
							throw ex;

						switch (message) {
							case ClientMessage.SubscriptionConfirmation confirmed:
								long caughtUp = confirmed.LastIndexedPosition;

								Log.Debug(
									"Subscription {subscriptionId} to $all:{eventFilter} confirmed. LastIndexedPosition is {position:N0}.",
									_subscriptionId, _eventFilter, caughtUp);

								confirmationPositionTcs.TrySetResult(new TFPos(caughtUp, caughtUp));
								return;
							case ClientMessage.SubscriptionDropped dropped:
								Log.Debug(
									"Subscription {subscriptionId} to $all:{eventFilter} dropped by subscription service: {droppedReason}",
									_subscriptionId, _eventFilter, dropped.Reason);
								switch (dropped.Reason) {
									case SubscriptionDropReason.AccessDenied:
										throw new ReadResponseException.AccessDenied();
									case SubscriptionDropReason.Unsubscribed:
										return;
									case SubscriptionDropReason.StreamDeleted: // applies only to regular streams
									case SubscriptionDropReason.NotFound: // applies only to persistent subscriptions
									default:
										throw ReadResponseException.UnknownError.Create(dropped.Reason);
								}
							case ClientMessage.StreamEventAppeared appeared: {
								Log.Verbose(
									"Subscription {subscriptionId} to $all:{eventFilter} received live event {position}.",
									_subscriptionId, _eventFilter, appeared.Event.OriginalPosition!.Value);

								if (!_liveEvents.Writer.TryWrite((++nextLiveSequenceNumber, appeared.Event, null))) {
									// this cannot happen because _liveEvents does not have full mode 'wait'.
									throw new Exception($"Unexpected error: could not write to live events channel for subscription {_subscriptionId} to $all:{_eventFilter}");
								}

								return;
							}
							case ClientMessage.CheckpointReached checkpointReached: {
								Log.Verbose(
									"Subscription {subscriptionId} to $all:{eventFilter} received live checkpoint {position}.",
									_subscriptionId, _eventFilter, checkpointReached.Position);

								if (!_liveEvents.Writer.TryWrite((++nextLiveSequenceNumber, null, checkpointReached.Position!.Value))) {
									// this cannot happen because _liveEvents does not have full mode 'wait'.
									throw new Exception(
										$"Unexpected error: could not write to live events channel for subscription {_subscriptionId} to $all:{_eventFilter}");
								}

								return;
							}
							default:
								throw ReadResponseException.UnknownMessage
									.Create<ClientMessage.SubscriptionConfirmation>(message);
						}
					} catch (Exception exception) {
						_liveEvents.Writer.TryComplete(exception);
						confirmationPositionTcs.TrySetException(exception);
					}
				}
			}

			private ValueTask ConfirmSubscription(CancellationToken ct) {
				return _channel.Writer.WriteAsync(new ReadResponse.SubscriptionConfirmed(SubscriptionId), ct);
			}

			private void ReadPage(TFPos startPos, IEnvelope envelope, CancellationToken ct) {
				Guid correlationId = Guid.NewGuid();
				Log.Verbose(
					"Subscription {subscriptionId} to $all:{eventFilter} reading next page starting from {position}.",
					_subscriptionId, _eventFilter, startPos);

				if (startPos is { CommitPosition: < 0, PreparePosition: < 0 })
					startPos = new TFPos(0, 0);

				_bus.Publish(new ClientMessage.FilteredReadAllEventsForward(
					correlationId, correlationId, envelope,
					startPos.CommitPosition, startPos.PreparePosition, ReadBatchSize, _resolveLinks, _requiresLeader,
					(int)_maxSearchWindow, null, _eventFilter, _user,
					replyOnExpired: true,
					expires: _expiryStrategy.GetExpiry(),
					cancellationToken: ct));
			}

			private void Unsubscribe() => _bus.Publish(new ClientMessage.UnsubscribeFromStream(Guid.NewGuid(),
				_subscriptionId, new NoopEnvelope(), _user));
		}
	}
}
