﻿using EventStore.ClientAPI.SystemData;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.UserManagement {
	[TestFixture, Category("ClientAPI"), Category("LongRunning")]
	public class list_users : TestWithNode {
		[Test]
		public async System.Threading.Tasks.Task list_all_users_worksAsync() {
			await _manager.CreateUserAsync("ouro", "ourofull", new[] { "foo", "bar" }, "ouro",
				new UserCredentials("admin", "changeit"));
			var x = await _manager.ListAllAsync(new UserCredentials("admin", "changeit"));
			Assert.AreEqual(3, x.Count);
			Assert.AreEqual("admin", x[0].LoginName);
			Assert.AreEqual("Event Store Administrator", x[0].FullName);
			Assert.AreEqual("ops", x[1].LoginName);
			Assert.AreEqual("Event Store Operations", x[1].FullName);
			Assert.AreEqual("ouro", x[2].LoginName);
			Assert.AreEqual("ourofull", x[2].FullName);
		}
	}
}
