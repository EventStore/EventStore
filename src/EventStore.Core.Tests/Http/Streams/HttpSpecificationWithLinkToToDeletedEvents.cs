using System;
using System.Text;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Common;
using EventStore.ClientAPI.SystemData;
using EventStore.Core.Tests.ClientAPI.Helpers;

namespace EventStore.Core.Tests.Http.Streams {
	public abstract class HttpSpecificationWithLinkToToDeletedEvents : HttpBehaviorSpecification {
		protected string LinkedStreamName;
		protected string DeletedStreamName;

		protected override void Given() {
			var creds = DefaultData.AdminCredentials;
			LinkedStreamName = Guid.NewGuid().ToString();
			DeletedStreamName = Guid.NewGuid().ToString();
			using (var conn = TestConnection.Create(_node.TcpEndPoint)) {
				conn.ConnectAsync().Wait();
				conn.AppendToStreamAsync(DeletedStreamName, ExpectedVersion.Any, creds,
						new EventData(Guid.NewGuid(), "testing", true, Encoding.UTF8.GetBytes("{'foo' : 4}"),
							new byte[0]))
					.Wait();
				conn.AppendToStreamAsync(LinkedStreamName, ExpectedVersion.Any, creds,
					new EventData(Guid.NewGuid(), SystemEventTypes.LinkTo, false,
						Encoding.UTF8.GetBytes("0@" + DeletedStreamName), new byte[0])).Wait();
				conn.DeleteStreamAsync(DeletedStreamName, ExpectedVersion.Any).Wait();
			}
		}
	}
}
