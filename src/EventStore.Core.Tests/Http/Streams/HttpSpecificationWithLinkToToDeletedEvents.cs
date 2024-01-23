extern alias GrpcClient;
extern alias GrpcClientStreams;
using GrpcClient::EventStore.Client;
using GrpcClientStreams::EventStore.Client;
using System;
using System.Text;
using System.Threading.Tasks;
using EventStore.Core.Tests.ClientAPI.Helpers;

namespace EventStore.Core.Tests.Http.Streams {
	public abstract class HttpSpecificationWithLinkToToDeletedEvents<TLogFormat, TStreamId>
		: HttpBehaviorSpecification<TLogFormat, TStreamId> {
		protected string LinkedStreamName;
		protected string DeletedStreamName;

		protected override async Task Given() {
			var creds = DefaultData.AdminCredentials;
			LinkedStreamName = Guid.NewGuid().ToString();
			DeletedStreamName = Guid.NewGuid().ToString();
			using (var conn = new GrpcEventStoreConnection(_node.HttpEndPoint)) {
				await conn.ConnectAsync();
				await conn.AppendToStreamAsync(DeletedStreamName, ExpectedVersion.Any,
						new[] {new EventData(Uuid.NewUuid(), "testing", Encoding.UTF8.GetBytes("{'foo' : 4}"),
							new byte[0])}, userCredentials: creds)
;
				await conn.AppendToStreamAsync(LinkedStreamName, ExpectedVersion.Any,
					new[] {new EventData(Uuid.NewUuid(), SystemEventTypes.LinkTo, 
						Encoding.UTF8.GetBytes("0@" + DeletedStreamName), new byte[0])}, userCredentials: creds);
				await conn.DeleteStreamAsync(DeletedStreamName, ExpectedVersion.Any);
			}
		}
	}
}
