using System;
using System.Security.Claims;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using Grpc.Core;
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
			private readonly CancellationToken _cancellationToken;
			private readonly TaskCompletionSource<bool> _subscriptionStarted;
			private readonly Channel<ResolvedEvent> _channel;
			private readonly SemaphoreSlim _semaphore;

			private ResolvedEvent? _current;
			private bool _disposed;

			public ResolvedEvent Current => _current.GetValueOrDefault();
			public Task Started => _subscriptionStarted.Task;
			public string SubscriptionId { get; }

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
				_cancellationToken = cancellationToken;
				_subscriptionStarted = new TaskCompletionSource<bool>();
				_channel = Channel.CreateBounded<ResolvedEvent>(BoundedChannelOptions);
				_semaphore = new SemaphoreSlim(1, 1);

				SubscriptionId = _subscriptionId.ToString();

				Subscribe(startRevision == StreamRevision.End
					? StreamRevision.FromInt64(readIndex.GetStreamLastEventNumber(_streamName) + 1)
					: startRevision + 1 ?? StreamRevision.Start, startRevision != StreamRevision.End);
			}

			public ValueTask DisposeAsync() {
				if (_disposed) {
					return new ValueTask(Task.CompletedTask);
				}

				Log.Information("Live subscription {subscriptionId} to {streamName} disposed.", _subscriptionId,
					_streamName);
				_disposed = true;
				_channel.Writer.TryComplete();
				Unsubscribe();
				return new ValueTask(Task.CompletedTask);
			}

			public async ValueTask<bool> MoveNextAsync() {
				ReadLoop:
				try {
					var @event = await _channel.Reader.ReadAsync(_cancellationToken).ConfigureAwait(false);
					if (_current.HasValue &&
					    @event.OriginalEvent.EventNumber <= _current.Value.OriginalEvent.EventNumber) {
						Log.Verbose(
							"Subscription {subscriptionId} to {streamName} skipping event {streamRevision}.",
							_subscriptionId, _streamName, @event.OriginalEvent.EventNumber);

						goto ReadLoop;
					}

					Log.Verbose(
						"Subscription {subscriptionId} to {streamName} seen event {streamRevision}.",
						_subscriptionId, _streamName, @event.OriginalEvent.EventNumber);

					_current = @event;
					return true;
				} catch (Exception ex) when (!ShouldIgnore(ex)) {
					if (ex.InnerException is RpcException) {
						throw ex.InnerException;
					}

					Log.Error(ex, "Subscription {subscriptionId} to {streamName} failed unexpectedly.",
						_subscriptionId, _streamName);

					throw;
				}
			}

			private void Subscribe(StreamRevision startRevision, bool catchUp) {
				if (catchUp) {
					CatchUp(startRevision);
				} else {
					GoLive(startRevision);
				}
			}

			private void CatchUp(StreamRevision startRevision) {
				Log.Information(
					"Catch-up subscription {subscriptionId} to {streamName}@{streamRevision} running...",
					_subscriptionId, _streamName, startRevision);

				ReadPage(startRevision, OnMessage);

				async Task OnMessage(Message message, CancellationToken ct) {
					if (message is ClientMessage.NotHandled notHandled &&
					    RpcExceptions.TryHandleNotHandled(notHandled, out var ex)) {
						_channel.Writer.TryComplete(ex);
						return;
					}

					if (!(message is ClientMessage.ReadStreamEventsForwardCompleted completed)) {
						_channel.Writer.TryComplete(
							RpcExceptions.UnknownMessage<ClientMessage.ReadStreamEventsForwardCompleted>(message));
						return;
					}

					switch (completed.Result) {
						case ReadStreamResult.Success:
							ConfirmSubscription();
							foreach (var @event in completed.Events) {
								var streamRevision = StreamRevision.FromInt64(@event.OriginalEvent.EventNumber);

								Log.Verbose(
									"Catch-up subscription {subscriptionId} to {streamName} received event {streamRevision}.",
									_subscriptionId, _streamName, streamRevision);

								await _channel.Writer.WriteAsync(@event, ct).ConfigureAwait(false);
							}

							if (completed.IsEndOfStream) {
								GoLive(StreamRevision.FromInt64(completed.NextEventNumber));
								return;
							}

							ReadPage(StreamRevision.FromInt64(completed.NextEventNumber), OnMessage);
							return;
						case ReadStreamResult.NoStream:
							ConfirmSubscription();
							await Task.Delay(TimeSpan.FromMilliseconds(50), ct).ConfigureAwait(false);
							ReadPage(startRevision, OnMessage);
							return;
						case ReadStreamResult.StreamDeleted:
							_channel.Writer.TryComplete(RpcExceptions.StreamDeleted(_streamName));
							return;
						case ReadStreamResult.AccessDenied:
							_channel.Writer.TryComplete(RpcExceptions.AccessDenied());
							return;
						default:
							_channel.Writer.TryComplete(RpcExceptions.UnknownError(completed.Result));
							return;
					}
				}
			}

			private void GoLive(StreamRevision startRevision) {
				var liveEvents = Channel.CreateBounded<ResolvedEvent>(BoundedChannelOptions);
				var caughtUpSource = new TaskCompletionSource<StreamRevision>();
				var liveMessagesCancelled = 0;

				Log.Information(
					"Live subscription {subscriptionId} to {streamName} running from {streamRevision}...",
					_subscriptionId, _streamName, startRevision);

				_bus.Publish(new ClientMessage.SubscribeToStream(Guid.NewGuid(), _subscriptionId,
					new ContinuationEnvelope(OnSubscriptionMessage, _semaphore, _cancellationToken), _subscriptionId,
					_streamName, _resolveLinks, _user));

				Task.Factory.StartNew(PumpLiveMessages, _cancellationToken);

				async Task PumpLiveMessages() {
					await caughtUpSource.Task.ConfigureAwait(false);
					await foreach (var @event in liveEvents.Reader.ReadAllAsync(_cancellationToken)
						.ConfigureAwait(false)) {
						await _channel.Writer.WriteAsync(@event, _cancellationToken).ConfigureAwait(false);
					}
				}

				async Task OnSubscriptionMessage(Message message, CancellationToken ct) {
					if (message is ClientMessage.NotHandled notHandled &&
					    RpcExceptions.TryHandleNotHandled(notHandled, out var ex)) {
						_channel.Writer.TryComplete(ex);
						return;
					}

					switch (message) {
						case ClientMessage.SubscriptionConfirmation confirmed:
							ConfirmSubscription();
							var caughtUp = StreamRevision.FromInt64(confirmed.LastEventNumber.Value);
							Log.Verbose(
								"Live subscription {subscriptionId} to {streamName} confirmed at {streamRevision}.",
								_subscriptionId, _streamName, caughtUp);
							ReadHistoricalEvents(startRevision);

							async Task OnHistoricalEventsMessage(Message message, CancellationToken ct) {
								if (message is ClientMessage.NotHandled notHandled &&
								    RpcExceptions.TryHandleNotHandled(notHandled, out var ex)) {
									_channel.Writer.TryComplete(ex);
									return;
								}

								if (!(message is ClientMessage.ReadStreamEventsForwardCompleted completed)) {
									_channel.Writer.TryComplete(
										RpcExceptions.UnknownMessage<ClientMessage.ReadStreamEventsForwardCompleted>(
											message));
									return;
								}

								switch (completed.Result) {
									case ReadStreamResult.Success:
										if (completed.Events.Length == 0 && completed.IsEndOfStream) {
											NotifyCaughtUp(StreamRevision.FromInt64(completed.FromEventNumber));
											return;
										}

										foreach (var @event in completed.Events) {
											var streamRevision = StreamRevision.FromInt64(@event.OriginalEventNumber);
											if (streamRevision > caughtUp) {
												NotifyCaughtUp(streamRevision);
												return;
											}

											await _channel.Writer.WriteAsync(@event, _cancellationToken)
												.ConfigureAwait(false);
										}

										ReadHistoricalEvents(StreamRevision.FromInt64(completed.NextEventNumber));

										void NotifyCaughtUp(StreamRevision streamRevision) {
											Log.Verbose(
												"Live subscription {subscriptionId} to {streamName} caught up at {streamRevision} because the end of stream was reached.",
												_subscriptionId, _streamName, streamRevision);
											caughtUpSource.TrySetResult(caughtUp);
										}

										return;
									case ReadStreamResult.NoStream:
										Log.Verbose(
											"Live subscription {subscriptionId} to {streamName} stream not found.",
											_subscriptionId, _streamName);
										ReadHistoricalEvents(startRevision);
										return;
									case ReadStreamResult.StreamDeleted:
										Log.Verbose(
											"Live subscription {subscriptionId} to {streamName} stream deleted.",
											_subscriptionId, _streamName);
										_channel.Writer.TryComplete(RpcExceptions.StreamDeleted(_streamName));
										return;
									case ReadStreamResult.AccessDenied:
										_channel.Writer.TryComplete(RpcExceptions.AccessDenied());
										return;
									default:
										_channel.Writer.TryComplete(RpcExceptions.UnknownError(completed.Result));
										return;
								}
							}

							void ReadHistoricalEvents(StreamRevision fromStreamRevision) {
								if (fromStreamRevision == StreamRevision.End) {
									throw new ArgumentOutOfRangeException(nameof(fromStreamRevision));
								}

								Log.Verbose(
									"Live subscription {subscriptionId} to {streamName} loading any missed events starting from {streamRevision}.",
									_subscriptionId, _streamName, fromStreamRevision);

								ReadPage(fromStreamRevision, OnHistoricalEventsMessage);
							}

							return;
						case ClientMessage.SubscriptionDropped dropped:
							Log.Debug(
								"Live subscription {subscriptionId} to {streamName} dropped: {droppedReason}",
								_subscriptionId, _streamName, dropped.Reason);
							switch (dropped.Reason) {
								case SubscriptionDropReason.AccessDenied:
									_channel.Writer.TryComplete(RpcExceptions.AccessDenied());
									return;
								case SubscriptionDropReason.NotFound:
									_channel.Writer.TryComplete(RpcExceptions.StreamNotFound(_streamName));
									return;
								case SubscriptionDropReason.Unsubscribed:
									return;
								default:
									_channel.Writer.TryComplete(RpcExceptions.UnknownError(dropped.Reason));
									return;
							}
						case ClientMessage.StreamEventAppeared appeared: {
							if (liveMessagesCancelled == 1) {
								return;
							}

							using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(1));
							try {
								Log.Verbose(
									"Live subscription {subscriptionId} to {streamName} received event {streamRevision}.",
									_subscriptionId, _streamName, appeared.Event.OriginalEventNumber);

								await liveEvents.Writer.WriteAsync(appeared.Event, cts.Token)
									.ConfigureAwait(false);
							} catch (Exception e) {
								if (Interlocked.Exchange(ref liveMessagesCancelled, 1) != 0) return;

								Log.Verbose(
									e,
									"Live subscription {subscriptionId} to {streamName} timed out at {streamRevision}; unsubscribing...",
									_subscriptionId, _streamName,
									StreamRevision.FromInt64(appeared.Event.OriginalEventNumber));

								Unsubscribe();

								liveEvents.Writer.Complete();

								CatchUp(StreamRevision.FromInt64(
									_current.GetValueOrDefault().OriginalEvent.EventNumber));
							}

							return;
						}
						default:
							_channel.Writer.TryComplete(
								RpcExceptions.UnknownMessage<ClientMessage.SubscriptionConfirmation>(message));
							return;
					}
				}
			}

			private void ConfirmSubscription() {
				if (_subscriptionStarted.Task.IsCompletedSuccessfully) return;
				_subscriptionStarted.TrySetResult(true);
			}

			private void ReadPage(StreamRevision startRevision, Func<Message, CancellationToken, Task> onMessage) {
				Guid correlationId = Guid.NewGuid();
				Log.Verbose(
					"Subscription {subscriptionId} to {streamName} reading next page starting from {nextRevision}.",
					_subscriptionId, _streamName, startRevision);

				_bus.Publish(new ClientMessage.ReadStreamEventsForward(
					correlationId, correlationId, new ContinuationEnvelope(onMessage, _semaphore, _cancellationToken),
					_streamName, startRevision.ToInt64(), ReadBatchSize, _resolveLinks, _requiresLeader, default,
					_user));
			}

			private void Unsubscribe() => _bus.Publish(new ClientMessage.UnsubscribeFromStream(Guid.NewGuid(),
				_subscriptionId, new NoopEnvelope(), _user));
		}
	}
}
