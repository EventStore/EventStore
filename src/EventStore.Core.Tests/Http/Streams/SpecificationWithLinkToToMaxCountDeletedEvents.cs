extern alias GrpcClient;
extern alias GrpcClientStreams;
using System;
using System.Text;
using System.Threading.Tasks;
using EventStore.Core.Tests.ClientAPI.Helpers;
using GrpcClient::EventStore.Client;
using StreamMetadata = GrpcClientStreams::EventStore.Client.StreamMetadata;
using SystemEventTypes = GrpcClientStreams::EventStore.Client.SystemEventTypes;

namespace EventStore.Core.Tests.Http.Streams {
	public abstract class SpecificationWithLinkToToMaxCountDeletedEvents<TLogFormat, TStreamId>
		: HttpBehaviorSpecification<TLogFormat, TStreamId> {
		protected string LinkedStreamName;
		protected string DeletedStreamName;

		protected override async Task Given() {
			var creds = DefaultData.AdminCredentials;
			DeletedStreamName = Guid.NewGuid().ToString();
			LinkedStreamName = Guid.NewGuid().ToString();
			using (var conn = new GrpcEventStoreConnection(_node.HttpEndPoint)) {
				await conn.ConnectAsync();
				await conn.AppendToStreamAsync(DeletedStreamName, ExpectedVersion.Any,
						new[] {new EventData(Uuid.NewUuid(), "testing1", Encoding.UTF8.GetBytes("{'foo' : 4}"),
							new byte[0])}, creds);
				await conn.SetStreamMetadataAsync(DeletedStreamName, ExpectedVersion.Any,
					new StreamMetadata(maxCount: 2));
				await conn.AppendToStreamAsync(DeletedStreamName, ExpectedVersion.Any,
						new[] {new EventData(Uuid.NewUuid(), "testing2", Encoding.UTF8.GetBytes("{'foo' : 4}"),
							new byte[0])}, creds);
				await conn.AppendToStreamAsync(DeletedStreamName, ExpectedVersion.Any,
						new[] {new EventData(Uuid.NewUuid(), "testing3", Encoding.UTF8.GetBytes("{'foo' : 4}"),
							new byte[0])}, creds);
				await conn.AppendToStreamAsync(LinkedStreamName, ExpectedVersion.Any,
					new[] {new EventData(Uuid.NewUuid(), SystemEventTypes.LinkTo,
						Encoding.UTF8.GetBytes("0@" + DeletedStreamName), new byte[0])}, creds);
			}
		}
	}
}
