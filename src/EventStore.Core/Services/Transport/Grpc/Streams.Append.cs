using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security.Claims;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Client.Shared;
using EventStore.Client.Streams;
using Google.Protobuf;
using Grpc.Core;

namespace EventStore.Core.Services.Transport.Grpc {
	internal partial class Streams<TStreamId> {
		public override async Task<AppendResp> Append(
			IAsyncStreamReader<AppendReq> requestStream,
			ServerCallContext context) {
			if (!await requestStream.MoveNext().ConfigureAwait(false))
				throw new InvalidOperationException();

			if (requestStream.Current.ContentCase != AppendReq.ContentOneofCase.Options)
				throw new InvalidOperationException();

			var options = requestStream.Current.Options;
			var streamName = options.StreamIdentifier;

			var expectedVersion = options.ExpectedStreamRevisionCase switch {
				AppendReq.Types.Options.ExpectedStreamRevisionOneofCase.Revision => new StreamRevision(
					options.Revision).ToInt64(),
				AppendReq.Types.Options.ExpectedStreamRevisionOneofCase.Any => AnyStreamRevision.Any.ToInt64(),
				AppendReq.Types.Options.ExpectedStreamRevisionOneofCase.StreamExists => AnyStreamRevision.StreamExists.ToInt64(),
				AppendReq.Types.Options.ExpectedStreamRevisionOneofCase.NoStream => AnyStreamRevision.NoStream.ToInt64(),
				_ => throw new InvalidOperationException()
			};

			var requiresLeader = GetRequiresLeader(context.RequestHeaders);

			var user = context.GetHttpContext().User;
			var op = WriteOperation.WithParameter(Plugins.Authorization.Operations.Streams.Parameters.StreamId(streamName));
			if (!await _provider.CheckAccessAsync(user, op, context.CancellationToken).ConfigureAwait(false)) {
				throw AccessDenied();
			}

			var correlationId = Guid.NewGuid(); // TODO: JPB use request id?

			var events = new List<Event>();

			var size = 0;
			while (await requestStream.MoveNext().ConfigureAwait(false)) {
				if (requestStream.Current.ContentCase != AppendReq.ContentOneofCase.ProposedMessage)
					throw new InvalidOperationException();

				var proposedMessage = requestStream.Current.ProposedMessage;
				var data = proposedMessage.Data.ToByteArray();
				var metadata = proposedMessage.CustomMetadata.ToByteArray();

				if (!proposedMessage.Metadata.TryGetValue(Constants.Metadata.Type, out var eventType)) {
					throw RpcExceptions.RequiredMetadataPropertyMissing(Constants.Metadata.Type);
				}

				if (!proposedMessage.Metadata.TryGetValue(Constants.Metadata.ContentType, out var contentType)) {
					throw RpcExceptions.RequiredMetadataPropertyMissing(Constants.Metadata.ContentType);
				}

				size += Event.SizeOnDisk(eventType, data, metadata);

				if (size > _maxAppendSize) {
					throw RpcExceptions.MaxAppendSizeExceeded(_maxAppendSize);
				}

				events.Add(new Event(
					Uuid.FromDto(proposedMessage.Id).ToGuid(),
					eventType,
					contentType == Constants.Metadata.ContentTypes.ApplicationJson,
					data,
					metadata));
			}

			var appendResponseSource = new TaskCompletionSource<AppendResp>();

			var envelope = new CallbackEnvelope(HandleWriteEventsCompleted);

			_publisher.Publish(new ClientMessage.WriteEvents(
				correlationId,
				correlationId,
				envelope,
				requiresLeader,
				streamName,
				expectedVersion,
				events.ToArray(),
				user,
				cancellationToken: context.CancellationToken));

			return await appendResponseSource.Task.ConfigureAwait(false);

			void HandleWriteEventsCompleted(Message message) {
				if (message is ClientMessage.NotHandled notHandled && RpcExceptions.TryHandleNotHandled(notHandled, out var ex)) {
					appendResponseSource.TrySetException(ex);
					return;
				}

				if (!(message is ClientMessage.WriteEventsCompleted completed)) {
					appendResponseSource.TrySetException(
						RpcExceptions.UnknownMessage<ClientMessage.WriteEventsCompleted>(message));
					return;
				}

				var response = new AppendResp();
				switch (completed.Result) {
					case OperationResult.Success:
						response.Success = new AppendResp.Types.Success();
						if (completed.LastEventNumber == -1) {
							response.Success.NoStream = new Empty();
						} else {
							response.Success.CurrentRevision = StreamRevision.FromInt64(completed.LastEventNumber);
						}

						if (completed.CommitPosition == -1) {
							response.Success.NoPosition = new Empty();
						} else {
							var position = Position.FromInt64(completed.CommitPosition, completed.PreparePosition);
							response.Success.Position = new AppendResp.Types.Position {
								CommitPosition = position.CommitPosition,
								PreparePosition = position.PreparePosition
							};
						}

						appendResponseSource.TrySetResult(response);
						return;
					case OperationResult.PrepareTimeout:
					case OperationResult.CommitTimeout:
					case OperationResult.ForwardTimeout:
						appendResponseSource.TrySetException(RpcExceptions.Timeout());
						return;
					case OperationResult.WrongExpectedVersion:
						response.WrongExpectedVersion = new AppendResp.Types.WrongExpectedVersion();

						switch (options.ExpectedStreamRevisionCase) {
							case AppendReq.Types.Options.ExpectedStreamRevisionOneofCase.Any:
								response.WrongExpectedVersion.ExpectedAny = new Empty();
								response.WrongExpectedVersion.Any2060 = new Empty();
								break;
							case AppendReq.Types.Options.ExpectedStreamRevisionOneofCase.StreamExists:
								response.WrongExpectedVersion.ExpectedStreamExists = new Empty();
								response.WrongExpectedVersion.StreamExists2060 = new Empty();
								break;
							case AppendReq.Types.Options.ExpectedStreamRevisionOneofCase.NoStream:
								response.WrongExpectedVersion.ExpectedNoStream = new Empty();
								response.WrongExpectedVersion.ExpectedRevision2060 = ulong.MaxValue;
								break;
							case AppendReq.Types.Options.ExpectedStreamRevisionOneofCase.Revision:
								response.WrongExpectedVersion.ExpectedRevision =
									StreamRevision.FromInt64(expectedVersion);
								response.WrongExpectedVersion.ExpectedRevision2060 =
									StreamRevision.FromInt64(expectedVersion);
								break;
						}

						if (completed.CurrentVersion == -1) {
							response.WrongExpectedVersion.CurrentNoStream = new Empty();
							response.WrongExpectedVersion.NoStream2060 = new Empty();
						} else {
							response.WrongExpectedVersion.CurrentRevision =
								StreamRevision.FromInt64(completed.CurrentVersion);
							response.WrongExpectedVersion.CurrentRevision2060 =
								StreamRevision.FromInt64(completed.CurrentVersion);
						}

						appendResponseSource.TrySetResult(response);
						return;
					case OperationResult.StreamDeleted:
						appendResponseSource.TrySetException(RpcExceptions.StreamDeleted(streamName));
						return;
					case OperationResult.InvalidTransaction:
						appendResponseSource.TrySetException(RpcExceptions.InvalidTransaction());
						return;
					case OperationResult.AccessDenied:
						appendResponseSource.TrySetException(RpcExceptions.AccessDenied());
						return;
					default:
						appendResponseSource.TrySetException(RpcExceptions.UnknownError(completed.Result));
						return;
				}
			}
		}

		public override Task Append2(IAsyncStreamReader<AppendReq2> requestStream, IServerStreamWriter<AppendResp2> responseStream, ServerCallContext context) {
			var channel = Channel.CreateUnbounded<Message>(new UnboundedChannelOptions() { AllowSynchronousContinuations = false, SingleReader = true, SingleWriter = true });
			return Task.WhenAll(PumpIn(requestStream, context, channel.Writer),
				PumpOut(responseStream, context, channel.Reader));

			async Task PumpIn(IAsyncStreamReader<AppendReq2> requestStream, ServerCallContext context, ChannelWriter<Message> responseChannel) {
				var envelope = new CallbackEnvelope(m => responseChannel.TryWrite(m));
				var requiresLeader = GetRequiresLeader(context.RequestHeaders);

				var user = context.GetHttpContext().User;

				Dictionary<Guid, ClientWriteRequest> pendingWrites = new Dictionary<Guid, ClientWriteRequest>();
				try {
					await foreach (var r in requestStream.ReadAllAsync(context.CancellationToken)) {
						var correlation = Uuid.FromDto(r.Correlation).ToGuid();
						switch (r.ContentCase) {
							case AppendReq2.ContentOneofCase.Options:

								pendingWrites.Add(correlation, FromOptions(correlation, r.Options));
								break;
							case AppendReq2.ContentOneofCase.ProposedMessage:
								if (pendingWrites.TryGetValue(correlation, out var pending)) {
									var proposedMessage = r.ProposedMessage;
									pending.Events.Add(FromProposedMessage(proposedMessage));
								}

								break;
							case AppendReq2.ContentOneofCase.Commit:
								if (pendingWrites.Remove(correlation, out var complete)) {
									_publisher.Publish(ToInternalMessage(complete, envelope, requiresLeader, user, CancellationToken.None));//TODO timeouts
								}
								break;
							case AppendReq2.ContentOneofCase.Batch:
								var b = r.Batch;
								if (b.Options != null && b.IsFinal) {
									var req = FromOptions(correlation, b.Options);
									var events = new List<Event>();
									foreach (var p in b.ProposedMessages) {
										req.Events.Add(FromProposedMessage(p));
									}

									_publisher.Publish(ToInternalMessage(req, envelope, requiresLeader, user, CancellationToken.None));
								} else if (b.Options == null) {
									//TODO: handle not found
									if (!b.IsFinal && pendingWrites.TryGetValue(correlation, out pending) || (b.IsFinal && pendingWrites.Remove(correlation, out pending))) {
										if (pending == null) throw new Exception("Huh");
										foreach (var p in b.ProposedMessages) {
											pending.Events.Add(FromProposedMessage(p));
										}

										if (b.IsFinal) {
											_publisher.Publish(ToInternalMessage(pending, envelope, requiresLeader, user, CancellationToken.None));
										}
									}
								}

								break;
							default:
								throw new ArgumentOutOfRangeException();
						}

					}

					responseChannel.TryComplete();
				} catch (Exception ex) {
					
					responseChannel.TryComplete(ex);
				}
				static ClientWriteRequest FromOptions(Guid correlation, AppendReq2.Types.Options options) {
					var streamName = options.StreamIdentifier;
					var expectedVersion = options.ExpectedStreamRevisionCase switch {
						AppendReq2.Types.Options.ExpectedStreamRevisionOneofCase.Revision => new StreamRevision(
							options.Revision).ToInt64(),
						AppendReq2.Types.Options.ExpectedStreamRevisionOneofCase.Any => AnyStreamRevision.Any.ToInt64(),
						AppendReq2.Types.Options.ExpectedStreamRevisionOneofCase.StreamExists => AnyStreamRevision.StreamExists.ToInt64(),
						AppendReq2.Types.Options.ExpectedStreamRevisionOneofCase.NoStream => AnyStreamRevision.NoStream.ToInt64(),
						_ => throw new InvalidOperationException()
					};
					return new ClientWriteRequest(correlation, streamName, expectedVersion);
				}

				static Event FromProposedMessage(AppendReq2.Types.ProposedMessage proposedMessage)
				{
					return new Event(Uuid.FromDto(proposedMessage.Id).ToGuid(),
						proposedMessage.Metadata[Constants.Metadata.Type],proposedMessage.Metadata[Constants.Metadata.ContentType] == Constants.Metadata.ContentTypes.ApplicationJson , proposedMessage.Data.ToByteArray(),
						proposedMessage.CustomMetadata.ToByteArray());
				}

				static ClientMessage.WriteEvents ToInternalMessage(ClientWriteRequest request, IEnvelope envelope, bool requiresLeader, ClaimsPrincipal user, CancellationToken token) {
					return new(Guid.NewGuid(), request.Correlation, envelope, requiresLeader,
						request.StreamId, request.ExpectedVersion, request.Events.ToArray(), user, cancellationToken:token);
				}
			}

			

			async Task PumpOut(IServerStreamWriter<AppendResp2> responseStream, ServerCallContext context,
				ChannelReader<Message> channelReader) {
				await foreach (var message in channelReader.ReadAllAsync(context.CancellationToken)) {
					if (message is ClientMessage.NotHandled notHandled && RpcExceptions.TryHandleNotHandled(notHandled, out var ex)) {
						throw ex;
					}

					if (!(message is ClientMessage.WriteEventsCompleted completed)) {
						throw RpcExceptions.UnknownMessage<ClientMessage.WriteEventsCompleted>(message);

					}

					if (completed.Result == OperationResult.WrongExpectedVersion) {
						await responseStream.WriteAsync(new AppendResp2() {
							Correlation = Uuid.FromGuid(completed.CorrelationId).ToDto(), WrongExpectedVersion = new AppendResp2.Types.WrongExpectedVersion() {


							}
						}).ConfigureAwait(false);
					} else if (completed.Result == OperationResult.Success) {
						await responseStream.WriteAsync(new AppendResp2() { Correlation = Uuid.FromGuid(completed.CorrelationId).ToDto(), Success = new AppendResp2.Types.Success() }).ConfigureAwait(false);
					}


				}
			}


			
		}

		class ClientWriteRequest {
			public Guid Correlation { get; }
			public string StreamId { get; }
			public long ExpectedVersion { get; }

			public List<Event> Events { get; }

			public ClientWriteRequest(Guid correlation, string streamId, long expectedVersion) {
				Correlation = correlation;
				StreamId = streamId;
				ExpectedVersion = expectedVersion;
				Events = new List<Event>();
			}


		}
	}
}
