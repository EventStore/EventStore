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
using Grpc.Core;
using Serilog;

namespace EventStore.Core.Services.Transport.Grpc {
	public partial class Enumerators {
		public class AllSubscription : ISubscriptionEnumerator {
			private static readonly ILogger Log = Serilog.Log.ForContext<AllSubscription>();

			private readonly Guid _subscriptionId;
			private readonly IPublisher _bus;
			private readonly bool _resolveLinks;
			private readonly ClaimsPrincipal _user;
			private readonly bool _requiresLeader;
			private readonly CancellationToken _cancellationToken;
			private readonly TaskCompletionSource<bool> _subscriptionStarted;
			private readonly TFPos _startPositionExclusive;
			private readonly Channel<ResolvedEvent> _channel;
			private readonly SemaphoreSlim _semaphore;

			private ResolvedEvent? _current;
			private bool _disposed;

			public ResolvedEvent Current => _current.GetValueOrDefault();
			public Task Started => _subscriptionStarted.Task;
			public string SubscriptionId { get; }

			public AllSubscription(IPublisher bus,
				Position? startPosition,
				bool resolveLinks,
				ClaimsPrincipal user,
				bool requiresLeader,
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
				_requiresLeader = requiresLeader;
				_cancellationToken = cancellationToken;
				_subscriptionStarted = new TaskCompletionSource<bool>();
				_channel = Channel.CreateBounded<ResolvedEvent>(BoundedChannelOptions);
				_semaphore = new SemaphoreSlim(1, 1);

				SubscriptionId = _subscriptionId.ToString();

				var startPositionExclusive = startPosition == Position.End
					? Position.FromInt64(readIndex.LastIndexedPosition, readIndex.LastIndexedPosition)
					: startPosition ?? Position.Start;

				_startPositionExclusive = new TFPos((long)startPositionExclusive.CommitPosition,
					(long)startPositionExclusive.PreparePosition);

				Subscribe(startPositionExclusive, startPosition != Position.End);
			}

			public ValueTask DisposeAsync() {
				if (_disposed) {
					return new ValueTask(Task.CompletedTask);
				}

				Log.Information("Subscription {subscriptionId} to $all disposed.", _subscriptionId);

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

				var @event = await _channel.Reader.ReadAsync(_cancellationToken).ConfigureAwait(false);

				if (@event.OriginalPosition.Value <= _startPositionExclusive || _current.HasValue &&
					@event.OriginalPosition.Value <= _current.Value.OriginalPosition.Value) {
					Log.Verbose(
						"Subscription {subscriptionId} to $all skipping event {position}.",
						_subscriptionId, @event.OriginalPosition.Value);
					goto ReadLoop;
				}

				Log.Verbose(
					"Subscription {subscriptionId} to $all seen event {position}.",
					_subscriptionId, @event.OriginalPosition.Value);

				_current = @event;

				return true;
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
					"Catch-up subscription {subscriptionId} to $all@{position} running...",
					_subscriptionId, startPosition);

				ReadPage(startPosition, OnMessage);

				async Task OnMessage(Message message, CancellationToken ct) {
					if (message is ClientMessage.NotHandled notHandled &&
						RpcExceptions.TryHandleNotHandled(notHandled, out var ex)) {
						Fail(ex);
						return;
					}

					if (!(message is ClientMessage.ReadAllEventsForwardCompleted completed)) {
						Fail(
							RpcExceptions.UnknownMessage<ClientMessage.ReadAllEventsForwardCompleted>(message));
						return;
					}

					switch (completed.Result) {
						case ReadAllResult.Success:
							ConfirmSubscription();

							foreach (var @event in completed.Events) {
								var position = Position.FromInt64(
									@event.OriginalPosition.Value.CommitPosition,
									@event.OriginalPosition.Value.PreparePosition);

								Log.Verbose(
									"Catch-up subscription {subscriptionId} to $all received event {position}.",
									_subscriptionId, position);

								await _channel.Writer.WriteAsync(@event, ct).ConfigureAwait(false);
							}

							if (completed.IsEndOfStream) {
								GoLive(Position.FromInt64(
									completed.NextPos.CommitPosition,
									completed.NextPos.PreparePosition));
								return;
							}

							ReadPage(Position.FromInt64(
								completed.NextPos.CommitPosition,
								completed.NextPos.PreparePosition), OnMessage);
							return;
						case ReadAllResult.AccessDenied:
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

				_bus.Publish(new ClientMessage.SubscribeToStream(Guid.NewGuid(), _subscriptionId,
					new ContinuationEnvelope(OnSubscriptionMessage, _semaphore, _cancellationToken), _subscriptionId,
					string.Empty, _resolveLinks, _user));

				Task.Factory.StartNew(PumpLiveMessages, _cancellationToken);

				async Task PumpLiveMessages() {
					await caughtUpSource.Task.ConfigureAwait(false);
					await foreach (var @event in liveEvents.Reader.ReadAllAsync(_cancellationToken)
						.ConfigureAwait(false)) {
						await _channel.Writer.WriteAsync(@event, _cancellationToken).ConfigureAwait(false);
					}
				}

				async Task OnSubscriptionMessage(Message message, CancellationToken cancellationToken) {
					if (message is ClientMessage.NotHandled notHandled &&
						RpcExceptions.TryHandleNotHandled(notHandled, out var ex)) {
						Fail(ex);
						return;
					}

					switch (message) {
						case ClientMessage.SubscriptionConfirmation confirmed:
							ConfirmSubscription();

							var caughtUp = new TFPos(confirmed.LastIndexedPosition,
								confirmed.LastIndexedPosition);
							Log.Verbose(
								"Live subscription {subscriptionId} to $all confirmed at {position}.",
								_subscriptionId, caughtUp);

							ReadHistoricalEvents(startPosition);

							async Task OnHistoricalEventsMessage(Message message, CancellationToken ct) {
								if (message is ClientMessage.NotHandled notHandled &&
									RpcExceptions.TryHandleNotHandled(notHandled, out var ex)) {
									Fail(ex);
									return;
								}

								if (!(message is ClientMessage.ReadAllEventsForwardCompleted completed)) {
									Fail(
										RpcExceptions.UnknownMessage<ClientMessage.ReadAllEventsForwardCompleted>(
											message));
									return;
								}

								switch (completed.Result) {
									case ReadAllResult.Success:
										if (completed.Events.Length == 0 && completed.IsEndOfStream) {
											NotifyCaughtUp(completed.CurrentPos);
											return;
										}

										foreach (var @event in completed.Events) {
											var position = @event.OriginalPosition.Value;

											if (position > caughtUp) {
												NotifyCaughtUp(position);
												return;
											}

											await _channel.Writer.WriteAsync(@event, _cancellationToken)
												.ConfigureAwait(false);
										}

										ReadHistoricalEvents(Position.FromInt64(
											completed.NextPos.CommitPosition,
											completed.NextPos.PreparePosition));
										return;

										void NotifyCaughtUp(TFPos position) {
											Log.Verbose(
												"Live subscription {subscriptionId} to $all caught up at {position} because the end of stream was reached.",
												_subscriptionId, position);
											caughtUpSource.TrySetResult(caughtUp);
										}
									case ReadAllResult.AccessDenied:
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
									"Live subscription {subscriptionId} to $all loading any missed events starting from {position}.",
									_subscriptionId, fromPosition);

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
										"Live subscription {subscriptionId} to $all enqueuing live message {position}.",
										_subscriptionId, appeared.Event.OriginalPosition);

									await liveEvents.Writer.WriteAsync(appeared.Event, cts.Token)
										.ConfigureAwait(false);
								} catch (Exception e) {
									if (Interlocked.Exchange(ref liveMessagesCancelled, 1) != 0)
										return;

									Log.Verbose(
										e,
										"Live subscription {subscriptionId} to $all timed out at {position}; unsubscribing...",
										_subscriptionId, appeared.Event.OriginalPosition.GetValueOrDefault());

									Unsubscribe();

									liveEvents.Writer.Complete();

									var originalPosition =
										_current.GetValueOrDefault().OriginalPosition.GetValueOrDefault();
									CatchUp(Position.FromInt64(
										originalPosition.CommitPosition,
										originalPosition.PreparePosition));
								}

								return;
							}
						default:
							Fail(
								RpcExceptions.UnknownMessage<ClientMessage.SubscriptionConfirmation>(message));
							return;
					}
				}

				void Fail(Exception exception) {
					this.Fail(exception);
					caughtUpSource.TrySetException(exception);
				}
			}

			private void ConfirmSubscription() {
				if (_subscriptionStarted.Task.IsCompletedSuccessfully)
					return;
				_subscriptionStarted.TrySetResult(true);
			}

			private void Fail(Exception exception) {
				_channel.Writer.TryComplete(exception);
				_subscriptionStarted.TrySetException(exception);
			}

			private void ReadPage(Position position, Func<Message, CancellationToken, Task> onMessage) {
				Guid correlationId = Guid.NewGuid();
				Log.Verbose(
					"Subscription {subscriptionId} to $all reading next page starting from {nextRevision}.",
					_subscriptionId, position);

				var (commitPosition, preparePosition) = position.ToInt64();

				_bus.Publish(new ClientMessage.ReadAllEventsForward(
					correlationId, correlationId, new ContinuationEnvelope(onMessage, _semaphore, _cancellationToken),
					commitPosition, preparePosition, ReadBatchSize, _resolveLinks, _requiresLeader, default,
					_user));
			}

			private void Unsubscribe() => _bus.Publish(new ClientMessage.UnsubscribeFromStream(Guid.NewGuid(),
				_subscriptionId, new NoopEnvelope(), _user));
		}
	}
}
