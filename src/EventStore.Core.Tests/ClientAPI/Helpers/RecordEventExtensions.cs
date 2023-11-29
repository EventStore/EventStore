extern alias GrpcClient;

using System.Text;
using GrpcClient::EventStore.Client;

namespace EventStore.Core.Tests.ClientAPI.Helpers;

public static class RecordEventExtensions {
	public static string DebugDataView(this EventRecord @event) {
		return Encoding.UTF8.GetString(@event.Data.ToArray());
	}

	public static string DebugMetadataView(this EventRecord @event) {
		return Encoding.UTF8.GetString(@event.Metadata.ToArray());
	}
}
