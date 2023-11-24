extern alias GrpcClient;
extern alias GrpcClientStreams;
using System;
using System.Text;
using System.Threading.Tasks;
using EventStore.Core.Tests.ClientAPI.Helpers;
using GrpcClient::EventStore.Client;
using SystemEventTypes = GrpcClientStreams::EventStore.Client.SystemEventTypes;

namespace EventStore.Core.Tests.Http.Streams {
	public abstract class HttpSpecificationWithLinkToToEvents<TLogFormat, TStreamId>
		: HttpBehaviorSpecification<TLogFormat, TStreamId> {
		protected string LinkedStreamName;
		protected string StreamName;
		protected string Stream2Name;

		protected override async Task Given() {
			var creds = DefaultData.AdminCredentials;
			LinkedStreamName = Guid.NewGuid().ToString();
			StreamName = Guid.NewGuid().ToString();
			Stream2Name = Guid.NewGuid().ToString();
			using (var conn = new GrpcEventStoreConnection(_node.HttpEndPoint)) {
				await conn.ConnectAsync();
				await conn.AppendToStreamAsync(StreamName, ExpectedVersion.Any,
						new[]{new EventData(Uuid.NewUuid(), "testing", Encoding.UTF8.GetBytes("{'foo' : 4}"),
							new byte[0])}, creds);
				await conn.AppendToStreamAsync(StreamName, ExpectedVersion.Any,
						new[]{new EventData(Uuid.NewUuid(), "testing", Encoding.UTF8.GetBytes("{'foo' : 4}"),
							new byte[0])}, creds);
				await conn.AppendToStreamAsync(Stream2Name, ExpectedVersion.Any,
					new[]{new EventData(Uuid.NewUuid(), "testing", Encoding.UTF8.GetBytes("{'foo' : 4}"),
						new byte[0])}, creds);
				await conn.AppendToStreamAsync(LinkedStreamName, ExpectedVersion.Any,
					new[]{new EventData(Uuid.NewUuid(), SystemEventTypes.LinkTo,
						Encoding.UTF8.GetBytes("0@" + Stream2Name), new byte[0])}, creds);
				await conn.AppendToStreamAsync(LinkedStreamName, ExpectedVersion.Any,
					new[]{new EventData(Uuid.NewUuid(), SystemEventTypes.LinkTo,
						Encoding.UTF8.GetBytes("1@" + StreamName), new byte[0])}, creds);
			}
		}
	}
}
