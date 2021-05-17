using System.Threading.Tasks;
using EventStore.ClientAPI.SystemData;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.UserManagement {
	[Category("ClientAPI"), Category("LongRunning")]
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	public class get_current_user<TLogFormat, TStreamId> : TestWithNode<TLogFormat, TStreamId> {
		[Test]
		public async Task returns_the_current_user() {
			var x = await _manager.GetCurrentUserAsync(new UserCredentials("admin", "changeit"));
			Assert.AreEqual("admin", x.LoginName);
			Assert.AreEqual("Event Store Administrator", x.FullName);
		}
	}
}
