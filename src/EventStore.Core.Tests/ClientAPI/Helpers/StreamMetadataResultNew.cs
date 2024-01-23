extern alias GrpcClientStreams;
extern alias GrpcClient;

using GrpcClient::EventStore.Client;
using GrpcClientStreams::EventStore.Client;

namespace EventStore.Core.Tests.ClientAPI.Helpers;

public class StreamMetadataResultNew {
	public string StreamName { get; init; }
	public bool StreamDeleted { get; init; }
	public StreamMetadata Metadata { get; init; }
	public StreamPosition? MetastreamRevision { get; init; }
}
