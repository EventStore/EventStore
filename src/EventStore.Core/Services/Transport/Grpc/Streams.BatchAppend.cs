using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using EventStore.Client.Streams;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Serilog;
using Status = Google.Rpc.Status;

namespace EventStore.Core.Services.Transport.Grpc {
	partial class Streams<TStreamId> {
		public override Task BatchAppend(IAsyncStreamReader<BatchAppendReq> requestStream,
			IServerStreamWriter<BatchAppendResp> responseStream, ServerCallContext context) {
			var channel =
				Channel.CreateUnbounded<(Message message, Guid correlationId, string streamId, long expectedVersion)>(
					new() {
						AllowSynchronousContinuations = false,
						SingleReader = true,
						SingleWriter = false
					});

			return Task.WhenAll(Receive(), Send());

			async Task Receive() {
				var pendingWrites = new ConcurrentDictionary<Guid, ClientWriteRequest>();

				var requiresLeader = GetRequiresLeader(context.RequestHeaders);

				var user = context.GetHttpContext().User;

				try {
					await foreach (var request in requestStream.ReadAllAsync(context.CancellationToken)) {
						try {
							var correlation = Uuid.FromDto(request.CorrelationId).ToGuid();

							if (request.Options != null) {
								var op = WriteOperation.WithParameter(
									Plugins.Authorization.Operations.Streams.Parameters.StreamId(
										request.Options.StreamIdentifier));
								if (!await _provider.CheckAccessAsync(user, op, context.CancellationToken)
									.ConfigureAwait(false)) {
									await responseStream.WriteAsync(new BatchAppendResp {
											CorrelationId = request.CorrelationId,
											StreamIdentifier = request.Options.StreamIdentifier,
											Error = Status.AccessDenied
										})
										.ConfigureAwait(false);
									continue;
								}

								if (request.Options.StreamIdentifier == null) {
									await responseStream.WriteAsync(new BatchAppendResp {
										CorrelationId = request.CorrelationId,
										StreamIdentifier = request.Options.StreamIdentifier,
										Error = Status.BadRequest(
											$"Required field {nameof(request.Options.StreamIdentifier)} not set.")
									}).ConfigureAwait(false);
									continue;
								}

								pendingWrites.AddOrUpdate(correlation,
									c => FromOptions(c, request.Options, context.CancellationToken),
									(_, writeRequest) => writeRequest);
							}

							if (!pendingWrites.TryGetValue(correlation, out var clientWriteRequest)) {
								continue;
							}

							clientWriteRequest.AddEvents(request.ProposedMessages.Select(FromProposedMessage));

							if (clientWriteRequest.Size > _maxAppendSize) {
								pendingWrites.TryRemove(correlation, out _);
								await responseStream.WriteAsync(new BatchAppendResp {
									CorrelationId = request.CorrelationId,
									StreamIdentifier = clientWriteRequest.StreamId,
									Error = Status.MaximumAppendSizeExceeded
								}).ConfigureAwait(false);
							}

							if (!request.IsFinal) {
								continue;
							}

							if (!pendingWrites.TryRemove(correlation, out _)) {
								continue;
							}

							_publisher.Publish(ToInternalMessage(clientWriteRequest, new CallbackEnvelope(message =>
									channel.Writer.TryWrite((message, clientWriteRequest.CorrelationId,
										clientWriteRequest.StreamId, clientWriteRequest.ExpectedVersion))),
								requiresLeader, user, CancellationToken.None));
						} catch (Exception ex) {
							await responseStream.WriteAsync(new BatchAppendResp {
								CorrelationId = request.CorrelationId,
								StreamIdentifier = request.Options.StreamIdentifier,
								Error = Status.BadRequest(ex.Message)
							}).ConfigureAwait(false);
						}
					}

					channel.Writer.TryComplete();
				} catch (Exception ex) {
					channel.Writer.TryComplete(ex);
					throw;
				}

				ClientWriteRequest FromOptions(Guid correlation, BatchAppendReq.Types.Options options,
					CancellationToken cancellationToken) =>
					new(correlation, options.StreamIdentifier, options.ExpectedStreamPositionCase switch {
						BatchAppendReq.Types.Options.ExpectedStreamPositionOneofCase.StreamPosition => new
							StreamRevision(options.StreamPosition).ToInt64(),
						BatchAppendReq.Types.Options.ExpectedStreamPositionOneofCase.Any => AnyStreamRevision
							.Any.ToInt64(),
						BatchAppendReq.Types.Options.ExpectedStreamPositionOneofCase.StreamExists => AnyStreamRevision
							.StreamExists.ToInt64(),
						BatchAppendReq.Types.Options.ExpectedStreamPositionOneofCase.NoStream => AnyStreamRevision
							.NoStream.ToInt64(),
						_ => throw new InvalidOperationException()
					}, Min(GetRequestedTimeout(options), _writeTimeout), () =>
						pendingWrites.TryRemove(correlation, out _)
							? channel.Writer.WriteAsync((new ClientMessage.WriteEventsCompleted(correlation,
									OperationResult.CommitTimeout, default),
								correlation, options.StreamIdentifier, 0), cancellationToken)
							: new ValueTask(Task.CompletedTask), cancellationToken);

				static Event FromProposedMessage(BatchAppendReq.Types.ProposedMessage proposedMessage) =>
					new(Uuid.FromDto(proposedMessage.Id).ToGuid(),
						proposedMessage.Metadata[Constants.Metadata.Type],
						proposedMessage.Metadata[Constants.Metadata.ContentType] ==
						Constants.Metadata.ContentTypes.ApplicationJson, proposedMessage.Data.ToByteArray(),
						proposedMessage.CustomMetadata.ToByteArray());

				static ClientMessage.WriteEvents ToInternalMessage(ClientWriteRequest request, IEnvelope envelope,
					bool requiresLeader, ClaimsPrincipal user, CancellationToken token) =>
					new(Guid.NewGuid(), request.CorrelationId, envelope, requiresLeader, request.StreamId,
						request.ExpectedVersion, request.Events.ToArray(), user, cancellationToken: token);

				static TimeSpan GetRequestedTimeout(BatchAppendReq.Types.Options options) =>
					(options.Deadline?.ToDateTime() ?? DateTime.MaxValue) - DateTime.UtcNow;

				static TimeSpan Min(TimeSpan a, TimeSpan b) => a > b ? b : a;
			}

			async Task Send() {
				try {
					await foreach (var (message, correlationId, streamId, expectedVersion) in channel.Reader.ReadAllAsync(
						context.CancellationToken)) {
						var batchAppendResp = message switch {
							ClientMessage.NotHandled notHandled => new BatchAppendResp {
								Error = new Status {
									Details = Any.Pack(new Empty()),
									Message = (notHandled.Reason, notHandled.AdditionalInfo) switch {
										(TcpClientMessageDto.NotHandled.NotHandledReason.NotReady, _) =>
											"Server Is Not Ready",
										(TcpClientMessageDto.NotHandled.NotHandledReason.TooBusy, _) =>
											"Server Is Busy",
										(TcpClientMessageDto.NotHandled.NotHandledReason.NotLeader or
											TcpClientMessageDto.NotHandled.NotHandledReason.IsReadOnly,
											TcpClientMessageDto.NotHandled.LeaderInfo
											leaderInfo) =>
											throw RpcExceptions.LeaderInfo(leaderInfo.HttpAddress, leaderInfo.HttpPort),
										(TcpClientMessageDto.NotHandled.NotHandledReason.NotLeader or
											TcpClientMessageDto.NotHandled.NotHandledReason.IsReadOnly, _) =>
											"No leader info available in response",
										_ =>
											$"Unknown {nameof(TcpClientMessageDto.NotHandled.NotHandledReason)} ({(int)notHandled.Reason})"
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
										expectedVersion)
								},
								OperationResult.AccessDenied => new BatchAppendResp {Error = Status.AccessDenied},
								OperationResult.StreamDeleted => new BatchAppendResp {
									Error = Status.StreamDeleted(streamId)
								},
								OperationResult.CommitTimeout or
									OperationResult.ForwardTimeout or
									OperationResult.PrepareTimeout => new BatchAppendResp {Error = Status.Timeout},
								_ => new BatchAppendResp {Error = Status.Unknown}
							},
							_ => new BatchAppendResp {
								Error = new Status {
									Details = Any.Pack(new Empty()),
									Message =
										$"Envelope callback expected either {nameof(ClientMessage.WriteEventsCompleted)} or {nameof(ClientMessage.NotHandled)}, received {message.GetType().Name} instead"
								}
							}
						};
						await responseStream.WriteAsync(new BatchAppendResp(batchAppendResp) {
							CorrelationId = Uuid.FromGuid(correlationId).ToDto(),
							StreamIdentifier = streamId,
						}).ConfigureAwait(false);
					}
				} catch (Exception ex) when(ex is not OperationCanceledException or TaskCanceledException) {
					Log.Warning(ex, string.Empty);
					throw;
				}
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
