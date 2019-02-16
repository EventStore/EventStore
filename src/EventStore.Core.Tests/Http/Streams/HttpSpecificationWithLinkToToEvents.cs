using System;
using System.Text;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Common;
using EventStore.ClientAPI.SystemData;
using EventStore.Core.Tests.ClientAPI.Helpers;

namespace EventStore.Core.Tests.Http.Streams {
	public abstract class HttpSpecificationWithLinkToToEvents : HttpBehaviorSpecification {
		protected string LinkedStreamName;
		protected string StreamName;
		protected string Stream2Name;

		protected override void Given() {
			var creds = DefaultData.AdminCredentials;
			LinkedStreamName = Guid.NewGuid().ToString();
			StreamName = Guid.NewGuid().ToString();
			Stream2Name = Guid.NewGuid().ToString();
			using (var conn = TestConnection.Create(_node.TcpEndPoint)) {
				conn.ConnectAsync().Wait();
				conn.AppendToStreamAsync(StreamName, ExpectedVersion.Any, creds,
						new EventData(Guid.NewGuid(), "testing", true, Encoding.UTF8.GetBytes("{'foo' : 4}"),
							new byte[0]))
					.Wait();
				conn.AppendToStreamAsync(StreamName, ExpectedVersion.Any, creds,
						new EventData(Guid.NewGuid(), "testing", true, Encoding.UTF8.GetBytes("{'foo' : 4}"),
							new byte[0]))
					.Wait();
				conn.AppendToStreamAsync(Stream2Name, ExpectedVersion.Any, creds,
						new EventData(Guid.NewGuid(), "testing", true, Encoding.UTF8.GetBytes("{'foo' : 4}"),
							new byte[0]))
					.Wait();
				conn.AppendToStreamAsync(LinkedStreamName, ExpectedVersion.Any, creds,
					new EventData(Guid.NewGuid(), SystemEventTypes.LinkTo, false,
						Encoding.UTF8.GetBytes("0@" + Stream2Name), new byte[0])).Wait();
				conn.AppendToStreamAsync(LinkedStreamName, ExpectedVersion.Any, creds,
					new EventData(Guid.NewGuid(), SystemEventTypes.LinkTo, false,
						Encoding.UTF8.GetBytes("1@" + StreamName), new byte[0])).Wait();
			}
		}
	}
}
