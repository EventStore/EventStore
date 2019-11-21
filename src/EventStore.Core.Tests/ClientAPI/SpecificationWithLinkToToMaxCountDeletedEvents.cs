using System;
using System.Text;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Common;
using EventStore.ClientAPI.SystemData;

namespace EventStore.Core.Tests.ClientAPI {
	public abstract class SpecificationWithLinkToToMaxCountDeletedEvents : SpecificationWithMiniNode {
		protected string LinkedStreamName;
		protected string DeletedStreamName;

		protected override async Task Given() {
			var creds = DefaultData.AdminCredentials;
			DeletedStreamName = Guid.NewGuid().ToString();
			LinkedStreamName = Guid.NewGuid().ToString();
			await _conn.AppendToStreamAsync(DeletedStreamName, ExpectedVersion.Any, creds,
				new EventData(Guid.NewGuid(), "testing1", true, Encoding.UTF8.GetBytes("{'foo' : 4}"), new byte[0]));
			await _conn.SetStreamMetadataAsync(DeletedStreamName, ExpectedVersion.Any,
				new StreamMetadata(2, null, null, null, null));
			await _conn.AppendToStreamAsync(DeletedStreamName, ExpectedVersion.Any, creds,
				new EventData(Guid.NewGuid(), "testing2", true, Encoding.UTF8.GetBytes("{'foo' : 4}"), new byte[0]));
			await _conn.AppendToStreamAsync(DeletedStreamName, ExpectedVersion.Any, creds,
				new EventData(Guid.NewGuid(), "testing3", true, Encoding.UTF8.GetBytes("{'foo' : 4}"), new byte[0]));
			await _conn.AppendToStreamAsync(LinkedStreamName, ExpectedVersion.Any, creds,
				new EventData(Guid.NewGuid(), SystemEventTypes.LinkTo, false,
					Encoding.UTF8.GetBytes("0@" + DeletedStreamName), new byte[0]));
		}
	}
}
