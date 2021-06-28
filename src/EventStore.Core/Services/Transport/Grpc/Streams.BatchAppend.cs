using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using EventStore.Client;
using EventStore.Client.Streams;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Plugins.Authorization;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Serilog;
using Empty = Google.Protobuf.WellKnownTypes.Empty;
using Status = Google.Rpc.Status;
using static EventStore.Core.Messages.TcpClientMessageDto.NotHandled;
using static EventStore.Client.Streams.BatchAppendReq.Types;
using static EventStore.Client.Streams.BatchAppendReq.Types.Options;

namespace EventStore.Core.Services.Transport.Grpc {
	partial class Streams<TStreamId> {
		public override async Task BatchAppend(IAsyncStreamReader<BatchAppendReq> requestStream,
			IServerStreamWriter<BatchAppendResp> responseStream, ServerCallContext context) {
			var worker = new BatchAppendWorker(_publisher, _provider, requestStream, responseStream,
				context.GetHttpContext().User, _maxAppendSize, _writeTimeout,
				GetRequiresLeader(context.RequestHeaders));

			await worker.Work(context.CancellationToken).ConfigureAwait(false);
		}

		private class BatchAppendWorker {
			private readonly IPublisher _publisher;
			private readonly IAuthorizationProvider _authorizationProvider;
			private readonly IAsyncStreamReader<BatchAppendReq> _requestStream;
			private readonly IServerStreamWriter<BatchAppendResp> _responseStream;
			private readonly ClaimsPrincipal _user;
			private readonly int _maxAppendSize;
			private readonly TimeSpan _writeTimeout;
			private readonly bool _requiresLeader;
			private readonly Channel<BatchAppendResp> _channel;

			private long _pending;

			public BatchAppendWorker(IPublisher publisher, IAuthorizationProvider authorizationProvider,
				IAsyncStreamReader<BatchAppendReq> requestStream, IServerStreamWriter<BatchAppendResp> responseStream,
				ClaimsPrincipal user, int maxAppendSize, TimeSpan writeTimeout, bool requiresLeader) {
				_publisher = publisher;
				_authorizationProvider = authorizationProvider;
				_requestStream = requestStream;
				_responseStream = responseStream;
				_user = user;
				_maxAppendSize = maxAppendSize;
				_writeTimeout = writeTimeout;
				_requiresLeader = requiresLeader;
				_channel = Channel.CreateUnbounded<BatchAppendResp>(new() {
					AllowSynchronousContinuations = false,
					SingleReader = false,
					SingleWriter = false
				});
			}

			public Task Work(CancellationToken cancellationToken) {
				var remaining = 2;
				var tcs = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);

				Send(_channel.Reader, cancellationToken)
					.ContinueWith(HandleCompletion, cancellationToken);
				Receive(_channel.Writer, _user, _requiresLeader, cancellationToken)
					.ContinueWith(HandleCompletion, cancellationToken);

				return tcs.Task;

				async void HandleCompletion(Task task) {
					try {
						await task.ConfigureAwait(false);
						if (Interlocked.Decrement(ref remaining) == 0) {
							tcs.TrySetResult();
						}
					} catch (OperationCanceledException) {
						tcs.TrySetCanceled(cancellationToken);
					} catch (Exception ex) {
						tcs.TrySetException(ex);
					}
				}
			}

			private async Task Send(ChannelReader<BatchAppendResp> reader, CancellationToken cancellationToken) {
				try {
					var isClosing = false;
					await foreach (var response in reader.ReadAllAsync(cancellationToken)) {
						if (!response.IsClosing) {
							await _responseStream.WriteAsync(response).ConfigureAwait(false);
							if (Interlocked.Decrement(ref _pending) >= 0 && isClosing) {
								break;
							}
						} else {
							isClosing = true;
						}
					}
				} catch (Exception ex) when (ex is not OperationCanceledException or TaskCanceledException) {
					Log.Warning(ex, string.Empty);

					throw;
				}
			}

			private async Task Receive(ChannelWriter<BatchAppendResp> writer, ClaimsPrincipal user, bool requiresLeader,
				CancellationToken cancellationToken) {
				var pendingWrites = new ConcurrentDictionary<Guid, ClientWriteRequest>();

				try {
					await foreach (var request in _requestStream.ReadAllAsync(cancellationToken)) {
						try {
							var correlationId = Uuid.FromDto(request.CorrelationId).ToGuid();

							if (request.Options != null) {
								if (!await _authorizationProvider.CheckAccessAsync(user, WriteOperation.WithParameter(
									Plugins.Authorization.Operations.Streams.Parameters.StreamId(
										request.Options.StreamIdentifier)), cancellationToken).ConfigureAwait(false)) {
									await writer.WriteAsync(new BatchAppendResp {
										CorrelationId = request.CorrelationId,
										StreamIdentifier = request.Options.StreamIdentifier,
										Error = Status.AccessDenied
									}, cancellationToken).ConfigureAwait(false);
									continue;
								}

								if (request.Options.StreamIdentifier == null) {
									await writer.WriteAsync(new BatchAppendResp {
										CorrelationId = request.CorrelationId,
										StreamIdentifier = request.Options.StreamIdentifier,
										Error = Status.BadRequest(
											$"Required field {nameof(request.Options.StreamIdentifier)} not set.")
									}, cancellationToken).ConfigureAwait(false);
									continue;
								}

								pendingWrites.AddOrUpdate(correlationId,
									c => FromOptions(c, request.Options, cancellationToken),
									(_, writeRequest) => writeRequest);
							}

							if (!pendingWrites.TryGetValue(correlationId, out var clientWriteRequest)) {
								continue;
							}

							clientWriteRequest.AddEvents(request.ProposedMessages.Select(FromProposedMessage));

							if (clientWriteRequest.Size > _maxAppendSize) {
								pendingWrites.TryRemove(correlationId, out _);
								await writer.WriteAsync(new BatchAppendResp {
									CorrelationId = request.CorrelationId,
									StreamIdentifier = clientWriteRequest.StreamId,
									Error = Status.MaximumAppendSizeExceeded((uint)_maxAppendSize)
								}, cancellationToken).ConfigureAwait(false);
							}

							if (!request.IsFinal) {
								continue;
							}

							if (!pendingWrites.TryRemove(correlationId, out _)) {
								continue;
							}

							Interlocked.Increment(ref _pending);

							_publisher.Publish(ToInternalMessage(clientWriteRequest, new CallbackEnvelope(message => {
								try {
									writer.TryWrite(ConvertMessage(message));
								} catch (Exception ex) {
									writer.TryComplete(ex);
								}
							}), requiresLeader, user, cancellationToken));

							BatchAppendResp ConvertMessage(Message message) {
								var batchAppendResp = message switch {
									ClientMessage.NotHandled notHandled => new BatchAppendResp {
										Error = new Status {
											Details = Any.Pack(new Empty()),
											Message = (notHandled.Reason, notHandled.AdditionalInfo) switch {
												(NotHandledReason.NotReady, _) => "Server Is Not Ready",
												(NotHandledReason.TooBusy, _) => "Server Is Busy",
												(NotHandledReason.NotLeader or NotHandledReason.IsReadOnly,
													LeaderInfo leaderInfo) =>
													throw RpcExceptions.LeaderInfo(leaderInfo.HttpAddress,
														leaderInfo.HttpPort),
												(NotHandledReason.NotLeader or NotHandledReason.IsReadOnly, _) =>
													"No leader info available in response",
												_ => $"Unknown {nameof(NotHandledReason)} ({(int)notHandled.Reason})"
											}
										}
									},
									ClientMessage.WriteEventsCompleted completed => completed.Result switch {
										OperationResult.Success => new BatchAppendResp {
											Success = BatchAppendResp.Types.Success.Completed(completed.CommitPosition,
												completed.PreparePosition, completed.LastEventNumber),
										},
										OperationResult.WrongExpectedVersion => new BatchAppendResp {
											Error = Status.WrongExpectedVersion(
												StreamRevision.FromInt64(completed.CurrentVersion),
												clientWriteRequest.ExpectedVersion)
										},
										OperationResult.AccessDenied => new BatchAppendResp
											{ Error = Status.AccessDenied },
										OperationResult.StreamDeleted => new BatchAppendResp {
											Error = Status.StreamDeleted(clientWriteRequest.StreamId)
										},
										OperationResult.CommitTimeout or
											OperationResult.ForwardTimeout or
											OperationResult.PrepareTimeout => new BatchAppendResp
												{ Error = Status.Timeout },
										_ => new BatchAppendResp { Error = Status.Unknown }
									},
									_ => new BatchAppendResp {
										Error = new Status {
											Details = Any.Pack(new Empty()),
											Message =
												$"Envelope callback expected either {nameof(ClientMessage.WriteEventsCompleted)} or {nameof(ClientMessage.NotHandled)}, received {message.GetType().Name} instead"
										}
									}
								};
								batchAppendResp.CorrelationId = Uuid.FromGuid(correlationId).ToDto();
								batchAppendResp.StreamIdentifier = new StreamIdentifier {
									StreamName = ByteString.CopyFromUtf8(clientWriteRequest.StreamId)
								};
								return batchAppendResp;
							}
						} catch (Exception ex) {
							await writer.WriteAsync(new BatchAppendResp {
								CorrelationId = request.CorrelationId,
								StreamIdentifier = request.Options.StreamIdentifier,
								Error = Status.BadRequest(ex.Message)
							}, cancellationToken).ConfigureAwait(false);
						}
					}

					await writer.WriteAsync(new BatchAppendResp {
						IsClosing = true
					}, cancellationToken).ConfigureAwait(false);
				} catch (Exception ex) {
					writer.TryComplete(ex);
					throw;
				}

				ClientWriteRequest FromOptions(Guid correlationId, Options options,
					CancellationToken cancellationToken) =>
					new(correlationId, options.StreamIdentifier, options.ExpectedStreamPositionCase switch {
						ExpectedStreamPositionOneofCase.StreamPosition => new StreamRevision(options.StreamPosition)
							.ToInt64(),
						ExpectedStreamPositionOneofCase.Any => AnyStreamRevision.Any.ToInt64(),
						ExpectedStreamPositionOneofCase.StreamExists => AnyStreamRevision.StreamExists.ToInt64(),
						ExpectedStreamPositionOneofCase.NoStream => AnyStreamRevision.NoStream.ToInt64(),
						_ => throw new InvalidOperationException()
					}, Min(GetRequestedTimeout(options), _writeTimeout), () =>
						pendingWrites.TryRemove(correlationId, out var pendingWrite)
							? writer.WriteAsync(new BatchAppendResp {
								CorrelationId = Uuid.FromGuid(correlationId).ToDto(),
								StreamIdentifier = new StreamIdentifier {
									StreamName = ByteString.CopyFromUtf8(pendingWrite.StreamId)
								},
								Error = Status.Timeout
							}, cancellationToken)
							: new ValueTask(Task.CompletedTask), cancellationToken);

				static Event FromProposedMessage(ProposedMessage proposedMessage) =>
					new(Uuid.FromDto(proposedMessage.Id).ToGuid(),
						proposedMessage.Metadata[Constants.Metadata.Type],
						proposedMessage.Metadata[Constants.Metadata.ContentType] ==
						Constants.Metadata.ContentTypes.ApplicationJson, proposedMessage.Data.ToByteArray(),
						proposedMessage.CustomMetadata.ToByteArray());

				static ClientMessage.WriteEvents ToInternalMessage(ClientWriteRequest request, IEnvelope envelope,
					bool requiresLeader, ClaimsPrincipal user, CancellationToken token) =>
					new(Guid.NewGuid(), request.CorrelationId, envelope, requiresLeader, request.StreamId,
						request.ExpectedVersion, request.Events.ToArray(), user, cancellationToken: token);

				static TimeSpan GetRequestedTimeout(Options options) =>
					(options.Deadline?.ToDateTime() ?? DateTime.MaxValue) - DateTime.UtcNow;

				static TimeSpan Min(TimeSpan a, TimeSpan b) => a > b ? b : a;
			}
		}

		private record ClientWriteRequest {
			public Guid CorrelationId { get; }
			public string StreamId { get; }
			public long ExpectedVersion { get; }
			private readonly List<Event> _events;
			public IEnumerable<Event> Events => _events.AsEnumerable();
			private int _size;
			public int Size => _size;

			public ClientWriteRequest(Guid correlationId, string streamId, long expectedVersion, TimeSpan timeout,
				Func<ValueTask> onTimeout, CancellationToken cancellationToken) {
				CorrelationId = correlationId;
				StreamId = streamId;
				_events = new List<Event>();
				_size = 0;
				ExpectedVersion = expectedVersion;

				if (Max(timeout, TimeSpan.Zero) == TimeSpan.Zero) {
					onTimeout();
				} else {
					Task.Delay(timeout, cancellationToken).ContinueWith(_ => onTimeout(), cancellationToken);
				}

				static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;
			}

			public ClientWriteRequest AddEvents(IEnumerable<Event> events) {
				foreach (var e in events) {
					_size += Event.SizeOnDisk(e.EventType, e.Data, e.Metadata);
					_events.Add(e);
				}

				return this;
			}
		}
	}
}
