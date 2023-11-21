extern alias GrpcClient;
extern alias GrpcClientStreams;

using System.Collections.Generic;
using System.Threading.Tasks;
using GrpcClient::EventStore.Client;
using EventData = GrpcClient::EventStore.Client.EventData;

namespace EventStore.Core.Tests.ClientAPI.Helpers;

public interface IEventStoreClient {
	Task<DeleteResult> DeleteStreamAsync(string stream, long expectedVersion, UserCredentials userCredentials = null) {
		return DeleteStreamAsync(stream, expectedVersion, false, userCredentials);
	}

	Task<DeleteResult> DeleteStreamAsync(string stream, long expectedVersion, bool hardDelete, UserCredentials userCredentials = null);

	Task<WriteResult> AppendToStreamAsync(string stream, long expectedVersion, params EventData[] events) {
		return AppendToStreamAsync(stream, expectedVersion, null, events);
	}

	public Task<WriteResult> AppendToStreamAsync(string stream, long expectedVersion, UserCredentials userCredentials,
		params EventData[] events) {
		return AppendToStreamAsync(stream, expectedVersion, events, userCredentials);
	}

	Task<WriteResult> AppendToStreamAsync(string stream, long expectedVersion, IEnumerable<EventData> events,
		UserCredentials userCredentials = null);

	Task<WriteResult> ConditionalAppendToStreamAsync(string stream, long expectedVersion, IEnumerable<EventData> events,
		UserCredentials userCredentials = null) {
		return AppendToStreamAsync(stream, expectedVersion, events, userCredentials);
	}

	Task<StreamEventsSliceNew> ReadStreamEventsForwardsAsync(string stream, long start, int count,
		bool resolveLinkTos,
		UserCredentials userCredentials = null);

	Task<StreamEventsSliceNew> ReadStreamEventsBackwardAsync(string stream, long start, int count,
		bool resolveLinkTos,
		UserCredentials userCredentials = null);

	public Task<AllEventsSliceNew> ReadAllEventsForwardAsync(Position position, int maxCount, bool resolveLinkTos,
		UserCredentials userCredentials = null);

	public Task<AllEventsSliceNew> ReadAllEventsBackwardAsync(Position position, int maxCount, bool resolveLinkTos,
		UserCredentials userCredentials = null);

	Task ConnectAsync();
	Task Close();
}
