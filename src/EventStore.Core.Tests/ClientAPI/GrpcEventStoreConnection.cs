using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using EventStore.Client;
using EventStore.Client.Streams;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.SystemData;
using EventStore.Core.Services.Transport.Grpc;
using Google.Protobuf;
using Grpc.Net.Client;
using Position = EventStore.ClientAPI.Position;
using Timeout = System.Threading.Timeout;

namespace EventStore.Core.Tests.ClientAPI;

public class GrpcEventStoreConnection : IEventStoreConnection {
	private const int MaxReceiveMessageLength = 17 * 1024 * 1024;
	readonly private Streams.StreamsClient _streamsClient;

	public GrpcEventStoreConnection(int port) {
		var httpClient = new HttpClient(CreateHandler(), true) {
			Timeout = Timeout.InfiniteTimeSpan,
			DefaultRequestVersion = new Version(2, 0),
		};

		var channel = GrpcChannel.ForAddress($"http://localhost:{port}", new GrpcChannelOptions {
			HttpClient = httpClient,
			DisposeHttpClient = true,
			MaxReceiveMessageSize = MaxReceiveMessageLength,
		});

		_streamsClient = new Streams.StreamsClient(channel);
	}

	HttpMessageHandler CreateHandler() {
		return new SocketsHttpHandler {
			EnableMultipleHttp2Connections = true
		};
	}

	public void Dispose() {
	}

	public Task ConnectAsync() {
	}

	public void Close() {
	}

	public Task<DeleteResult> DeleteStreamAsync(string stream, long expectedVersion, UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task<DeleteResult> DeleteStreamAsync(string stream, long expectedVersion, bool hardDelete, UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public async Task<WriteResult> AppendToStreamAsync(string stream, long expectedVersion, params EventData[] events) {
		WriteResult writeResult = default;
		using var call = _streamsClient.Append();
		var header = new AppendReq {
			Options = new AppendReq.Types.Options {
				StreamIdentifier = stream
			}
		};

		switch (expectedVersion) {
			case -4:
				header.Options.StreamExists = new Empty();
				break;
			case -2:
				header.Options.Any = new Empty();
				break;
			case -1:
				header.Options.NoStream = new Empty();
				break;
			default:
				header.Options.Revision = (ulong) expectedVersion;
				break;
		}

		try {
			await call.RequestStream.WriteAsync(header).ConfigureAwait(false);

			foreach (var e in events) {
				await call.RequestStream.WriteAsync(new AppendReq {
					ProposedMessage = new AppendReq.Types.ProposedMessage {
						Id = ToUUID(e.EventId),
						Data = ByteString.CopyFrom(e.Data),
						CustomMetadata = ByteString.CopyFrom(e.Metadata),
						Metadata = {
							{ "type", e.Type },
							{ "content-type", e.IsJson ? "application/json" : "application/octet-stream" }
						}
					}
				}).ConfigureAwait(false);
			}
		} finally {
			await call.RequestStream.CompleteAsync().ConfigureAwait(false);
			var response = await call.ResponseAsync.ConfigureAwait(false);

			if (response.Success != null) {
				var nextExpectedVersion = response.Success.CurrentRevisionOptionCase == AppendResp.Types.Success.CurrentRevisionOptionOneofCase.NoStream
					? -1
					: (long)response.Success.CurrentRevision;

				var position = response.Success.PositionOptionCase == AppendResp.Types.Success.PositionOptionOneofCase.Position
					? new Position((long)response.Success.Position.CommitPosition, (long)response.Success.Position.PreparePosition)
					: new Position(0, 0);

				writeResult = new WriteResult(nextExpectedVersion, position);
			} else {
				if (response.WrongExpectedVersion != null) {
					var actualStreamRevision = response.WrongExpectedVersion.CurrentRevisionOptionCase switch {
						AppendResp.Types.WrongExpectedVersion.CurrentRevisionOptionOneofCase.CurrentNoStream =>
							-1,
						_ => (long) response.WrongExpectedVersion.CurrentRevision
					};

					if (response.WrongExpectedVersion.ExpectedRevisionOptionCase == AppendResp.Types
						    .WrongExpectedVersion.ExpectedRevisionOptionOneofCase.ExpectedRevision) {
						throw new WrongExpectedVersionException(header.Options.StreamIdentifier!,
							(long)response.WrongExpectedVersion.ExpectedRevision,
							actualStreamRevision);
					}

					var expectedStreamState = response.WrongExpectedVersion.ExpectedRevisionOptionCase switch {
						AppendResp.Types.WrongExpectedVersion.ExpectedRevisionOptionOneofCase.ExpectedAny =>
							-2,
						AppendResp.Types.WrongExpectedVersion.ExpectedRevisionOptionOneofCase.ExpectedNoStream =>
							-1,
						AppendResp.Types.WrongExpectedVersion.ExpectedRevisionOptionOneofCase.ExpectedStreamExists =>
							-4,
						_ => -2,
					};

					throw new WrongExpectedVersionException(header.Options.StreamIdentifier!,
						expectedStreamState, actualStreamRevision);
				}
			}
		}

		return writeResult;
	}

	private UUID ToUUID(Guid value) {
		if (!BitConverter.IsLittleEndian) {
			throw new NotSupportedException();
		}

		Span<byte> data = stackalloc byte[16];

		if (!value.TryWriteBytes(data)) {
			throw new InvalidOperationException();
		}

		data.Slice(0, 8).Reverse();
		data.Slice(0, 2).Reverse();
		data.Slice(2, 2).Reverse();
		data.Slice(4, 4).Reverse();
		data.Slice(8).Reverse();

		return new UUID {
			Structured = new UUID.Types.Structured {
				MostSignificantBits = BitConverter.ToInt64(data),
				LeastSignificantBits = BitConverter.ToInt64(data.Slice(8))
			},
		};
	}

	public Task<WriteResult> AppendToStreamAsync(string stream, long expectedVersion, UserCredentials userCredentials,
		params EventData[] events) {
		throw new NotImplementedException();
	}

	public Task<WriteResult> AppendToStreamAsync(string stream, long expectedVersion, IEnumerable<EventData> events,
		UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task<ConditionalWriteResult> ConditionalAppendToStreamAsync(string stream, long expectedVersion, IEnumerable<EventData> events,
		UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task<EventStoreTransaction> StartTransactionAsync(string stream, long expectedVersion, UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public EventStoreTransaction ContinueTransaction(long transactionId, UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task<EventReadResult> ReadEventAsync(string stream, long eventNumber, bool resolveLinkTos, UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task<StreamEventsSlice> ReadStreamEventsForwardAsync(string stream, long start, int count, bool resolveLinkTos,
		UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task<StreamEventsSlice> ReadStreamEventsBackwardAsync(string stream, long start, int count, bool resolveLinkTos,
		UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task<AllEventsSlice> ReadAllEventsForwardAsync(Position position, int maxCount, bool resolveLinkTos,
		UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task<AllEventsSlice> FilteredReadAllEventsForwardAsync(Position position, int maxCount, bool resolveLinkTos, Filter filter,
		UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task<AllEventsSlice> FilteredReadAllEventsForwardAsync(Position position, int maxCount, bool resolveLinkTos, Filter filter,
		int maxSearchWindow, UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task<AllEventsSlice> ReadAllEventsBackwardAsync(Position position, int maxCount, bool resolveLinkTos,
		UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task<AllEventsSlice> FilteredReadAllEventsBackwardAsync(Position position, int maxCount, bool resolveLinkTos, Filter filter,
		int maxSearchWindow, UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task<AllEventsSlice> FilteredReadAllEventsBackwardAsync(Position position, int maxCount, bool resolveLinkTos, Filter filter,
		UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task<EventStoreSubscription> SubscribeToStreamAsync(string stream, bool resolveLinkTos, Func<EventStoreSubscription, ResolvedEvent, Task> eventAppeared, Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
		UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public EventStoreStreamCatchUpSubscription SubscribeToStreamFrom(string stream, long? lastCheckpoint,
		CatchUpSubscriptionSettings settings, Func<EventStoreCatchUpSubscription, ResolvedEvent, Task> eventAppeared, Action<EventStoreCatchUpSubscription> liveProcessingStarted = null,
		Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null, UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task<EventStoreSubscription> SubscribeToAllAsync(bool resolveLinkTos, Func<EventStoreSubscription, ResolvedEvent, Task> eventAppeared, Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
		UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task<EventStoreSubscription> FilteredSubscribeToAllAsync(bool resolveLinkTos, Filter filter, Func<EventStoreSubscription, ResolvedEvent, Task> eventAppeared, Func<EventStoreSubscription, Position, Task> checkpointReached,
		int checkpointInterval, Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null, UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task<EventStoreSubscription> FilteredSubscribeToAllAsync(bool resolveLinkTos, Filter filter, Func<EventStoreSubscription, ResolvedEvent, Task> eventAppeared,
		Action<EventStoreSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null, UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public EventStorePersistentSubscriptionBase ConnectToPersistentSubscription(string stream, string groupName,
		Func<EventStorePersistentSubscriptionBase, ResolvedEvent, int?, Task> eventAppeared, Action<EventStorePersistentSubscriptionBase, SubscriptionDropReason, Exception> subscriptionDropped = null, UserCredentials userCredentials = null, int bufferSize = 10,
		bool autoAck = true) {
		throw new NotImplementedException();
	}

	public Task<EventStorePersistentSubscriptionBase> ConnectToPersistentSubscriptionAsync(string stream, string groupName, Func<EventStorePersistentSubscriptionBase, ResolvedEvent, int?, Task> eventAppeared,
		Action<EventStorePersistentSubscriptionBase, SubscriptionDropReason, Exception> subscriptionDropped = null, UserCredentials userCredentials = null, int bufferSize = 10,
		bool autoAck = true) {
		throw new NotImplementedException();
	}

	public EventStoreAllCatchUpSubscription SubscribeToAllFrom(Position? lastCheckpoint, CatchUpSubscriptionSettings settings,
		Func<EventStoreCatchUpSubscription, ResolvedEvent, Task> eventAppeared, Action<EventStoreCatchUpSubscription> liveProcessingStarted = null, Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
		UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public EventStoreAllFilteredCatchUpSubscription FilteredSubscribeToAllFrom(Position? lastCheckpoint, Filter filter,
		CatchUpSubscriptionFilteredSettings settings, Func<EventStoreCatchUpSubscription, ResolvedEvent, Task> eventAppeared, Func<EventStoreCatchUpSubscription, Position, Task> checkpointReached,
		int checkpointIntervalMultiplier, Action<EventStoreCatchUpSubscription> liveProcessingStarted = null, Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null,
		UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public EventStoreAllFilteredCatchUpSubscription FilteredSubscribeToAllFrom(Position? lastCheckpoint, Filter filter,
		CatchUpSubscriptionFilteredSettings settings, Func<EventStoreCatchUpSubscription, ResolvedEvent, Task> eventAppeared, Action<EventStoreCatchUpSubscription> liveProcessingStarted = null,
		Action<EventStoreCatchUpSubscription, SubscriptionDropReason, Exception> subscriptionDropped = null, UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task UpdatePersistentSubscriptionAsync(string stream, string groupName, PersistentSubscriptionSettings settings,
		UserCredentials credentials) {
		throw new NotImplementedException();
	}

	public Task CreatePersistentSubscriptionAsync(string stream, string groupName, PersistentSubscriptionSettings settings,
		UserCredentials credentials) {
		throw new NotImplementedException();
	}

	public Task DeletePersistentSubscriptionAsync(string stream, string groupName, UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task<WriteResult> SetStreamMetadataAsync(string stream, long expectedMetastreamVersion, StreamMetadata metadata,
		UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task<WriteResult> SetStreamMetadataAsync(string stream, long expectedMetastreamVersion, byte[] metadata,
		UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task<StreamMetadataResult> GetStreamMetadataAsync(string stream, UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task<RawStreamMetadataResult> GetStreamMetadataAsRawBytesAsync(string stream, UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public Task SetSystemSettingsAsync(SystemSettings settings, UserCredentials userCredentials = null) {
		throw new NotImplementedException();
	}

	public string ConnectionName { get; }
	public ConnectionSettings Settings { get; }
	public event EventHandler<ClientConnectionEventArgs> Connected;
	public event EventHandler<ClientConnectionEventArgs> Disconnected;
	public event EventHandler<ClientReconnectingEventArgs> Reconnecting;
	public event EventHandler<ClientClosedEventArgs> Closed;
	public event EventHandler<ClientErrorEventArgs> ErrorOccurred;
	public event EventHandler<ClientAuthenticationFailedEventArgs> AuthenticationFailed;
}
