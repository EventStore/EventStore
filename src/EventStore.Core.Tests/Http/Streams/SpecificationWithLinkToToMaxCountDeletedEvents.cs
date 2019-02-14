using System;
using System.Text;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Common;
using EventStore.ClientAPI.SystemData;
using EventStore.Core.Tests.ClientAPI.Helpers;

namespace EventStore.Core.Tests.Http.Streams {
	public abstract class SpecificationWithLinkToToMaxCountDeletedEvents : HttpBehaviorSpecification {
		protected string LinkedStreamName;
		protected string DeletedStreamName;

		protected override void Given() {
			var creds = DefaultData.AdminCredentials;
			DeletedStreamName = Guid.NewGuid().ToString();
			LinkedStreamName = Guid.NewGuid().ToString();
			using (var conn = TestConnection.Create(_node.TcpEndPoint)) {
				conn.ConnectAsync().Wait();
				conn.AppendToStreamAsync(DeletedStreamName, ExpectedVersion.Any, creds,
						new EventData(Guid.NewGuid(), "testing1", true, Encoding.UTF8.GetBytes("{'foo' : 4}"),
							new byte[0]))
					.Wait();
				conn.SetStreamMetadataAsync(DeletedStreamName, ExpectedVersion.Any,
					new StreamMetadata(2, null, null, null, null)).Wait();
				conn.AppendToStreamAsync(DeletedStreamName, ExpectedVersion.Any, creds,
						new EventData(Guid.NewGuid(), "testing2", true, Encoding.UTF8.GetBytes("{'foo' : 4}"),
							new byte[0]))
					.Wait();
				conn.AppendToStreamAsync(DeletedStreamName, ExpectedVersion.Any, creds,
						new EventData(Guid.NewGuid(), "testing3", true, Encoding.UTF8.GetBytes("{'foo' : 4}"),
							new byte[0]))
					.Wait();
				conn.AppendToStreamAsync(LinkedStreamName, ExpectedVersion.Any, creds,
					new EventData(Guid.NewGuid(), SystemEventTypes.LinkTo, false,
						Encoding.UTF8.GetBytes("0@" + DeletedStreamName), new byte[0])).Wait();
			}
		}
	}
}
