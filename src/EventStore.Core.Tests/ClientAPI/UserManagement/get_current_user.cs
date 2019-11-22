using System.Threading.Tasks;
using EventStore.ClientAPI.SystemData;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.UserManagement {
	[TestFixture, Category("ClientAPI"), Category("LongRunning")]
	public class get_current_user : TestWithNode {
		[Test]
		public async Task returns_the_current_user() {
			var x = await _manager.GetCurrentUserAsync(new UserCredentials("admin", "changeit"));
			Assert.AreEqual("admin", x.LoginName);
			Assert.AreEqual("Event Store Administrator", x.FullName);
		}
	}
}
