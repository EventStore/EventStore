extern alias GrpcClient;
extern alias GrpcClientStreams;
using System;
using System.Text;
using System.Threading.Tasks;
using EventStore.Core.Tests.ClientAPI.Helpers;
using GrpcClient::EventStore.Client;
using StreamMetadata = GrpcClientStreams::EventStore.Client.StreamMetadata;
using SystemEventTypes = GrpcClientStreams::EventStore.Client.SystemEventTypes;

namespace EventStore.Core.Tests.ClientAPI {
	public abstract class SpecificationWithLinkToToMaxCountDeletedEvents<TLogFormat, TStreamId>
		: SpecificationWithMiniNode<TLogFormat, TStreamId> {
		protected string LinkedStreamName;
		protected string DeletedStreamName;

		protected override async Task Given() {
			var creds = DefaultData.AdminCredentials;
			DeletedStreamName = Guid.NewGuid().ToString();
			LinkedStreamName = Guid.NewGuid().ToString();
			await _conn.AppendToStreamAsync(DeletedStreamName, ExpectedVersion.Any, creds,
				new EventData(Uuid.NewUuid(), "testing1", Encoding.UTF8.GetBytes("{'foo' : 4}"), new byte[0]));
			await _conn.SetStreamMetadataAsync(DeletedStreamName, ExpectedVersion.Any,
				new StreamMetadata(maxCount: 2));
			await _conn.AppendToStreamAsync(DeletedStreamName, ExpectedVersion.Any, creds,
				new EventData(Uuid.NewUuid(), "testing2", Encoding.UTF8.GetBytes("{'foo' : 4}"), new byte[0]));
			await _conn.AppendToStreamAsync(DeletedStreamName, ExpectedVersion.Any, creds,
				new EventData(Uuid.NewUuid(), "testing3", Encoding.UTF8.GetBytes("{'foo' : 4}"), new byte[0]));
			await _conn.AppendToStreamAsync(LinkedStreamName, ExpectedVersion.Any, creds,
				new EventData(Uuid.NewUuid(), SystemEventTypes.LinkTo,
					Encoding.UTF8.GetBytes("0@" + DeletedStreamName), new byte[0], "application/octet-stream"));
		}
	}
}
