extern alias GrpcClient;
extern alias GrpcClientStreams;
extern alias GrpcClientPersistent;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GrpcClient::EventStore.Client;
using EventData = GrpcClient::EventStore.Client.EventData;
using PersistentSubscription = GrpcClientPersistent::EventStore.Client.PersistentSubscription;
using PersistentSubscriptionSettings = GrpcClientPersistent::EventStore.Client.PersistentSubscriptionSettings;
using StreamMetadata = GrpcClientStreams::EventStore.Client.StreamMetadata;

namespace EventStore.Core.Tests.ClientAPI.Helpers;

public interface IEventStoreClient {
	Task<EventReadResultNew> ReadEventAsync(string stream, long eventNumber, bool resolveLinkTos, UserCredentials userCredentials = null);

	Task<WriteResult> SetStreamMetadataAsync(
		string stream,
		long expectedMetaStreamVersion,
		StreamMetadata metadata,
		UserCredentials userCredentials = null);

	Task<DeleteResult> DeleteStreamAsync(string stream, long expectedVersion, UserCredentials userCredentials = null) {
		return DeleteStreamAsync(stream, expectedVersion, false, userCredentials);
	}

	Task<DeleteResult> DeleteStreamAsync(string stream, long expectedVersion, bool hardDelete, UserCredentials userCredentials = null);

	Task<WriteResult> AppendToStreamAsync(string stream, long expectedVersion, params EventData[] events) {
		return AppendToStreamAsync(stream, expectedVersion, null, events);
	}

	Task<WriteResult> AppendToStreamAsync(string stream, long expectedVersion, UserCredentials userCredentials,
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

	Task<AllEventsSliceNew> ReadAllEventsForwardAsync(Position position, int maxCount, bool resolveLinkTos,
		UserCredentials userCredentials = null);

	Task<AllEventsSliceNew> ReadAllEventsBackwardAsync(Position position, int maxCount, bool resolveLinkTos,
		UserCredentials userCredentials = null);

	Task CreatePersistentSubscriptionAsync(string stream, string groupName, PersistentSubscriptionSettings settings, UserCredentials userCredentials = null);

	Task<PersistentSubscription> ConnectToPersistentSubscription(
		string stream,
		string groupName,
		Func<PersistentSubscription, ResolvedEvent, int?, Task> eventAppeared,
		Action<PersistentSubscription, SubscriptionDroppedReason, Exception> subscriptionDropped = null,
		UserCredentials userCredentials = null,
		int bufferSize = 10,
		bool autoAck = true);

	Task ConnectAsync();
	Task Close();
}
