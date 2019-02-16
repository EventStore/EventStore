using System;
using EventStore.ClientAPI.SystemData;
using EventStore.ClientAPI.UserManagement;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.UserManagement {
	[TestFixture, Category("ClientAPI"), Category("LongRunning")]
	public class creating_a_user : TestWithNode {
		[Test]
		public void creating_a_user_with_null_username_throws() {
			Assert.Throws<ArgumentNullException>(() =>
				_manager.CreateUserAsync(null, "greg", new[] {"foo", "bar"}, "foofoofoo"));
		}

		[Test]
		public void creating_a_user_with_empty_username_throws() {
			Assert.Throws<ArgumentNullException>(() =>
				_manager.CreateUserAsync("", "ouro", new[] {"foo", "bar"}, "foofoofoo"));
		}

		[Test]
		public void creating_a_user_with_null_name_throws() {
			Assert.Throws<ArgumentNullException>(() =>
				_manager.CreateUserAsync("ouro", null, new[] {"foo", "bar"}, "foofoofoo"));
		}

		[Test]
		public void creating_a_user_with_empty_name_throws() {
			Assert.Throws<ArgumentNullException>(() =>
				_manager.CreateUserAsync("ouro", "", new[] {"foo", "bar"}, "foofoofoo"));
		}


		[Test]
		public void creating_a_user_with_null_password_throws() {
			Assert.Throws<ArgumentNullException>(() =>
				_manager.CreateUserAsync("ouro", "ouro", new[] {"foo", "bar"}, null));
		}

		[Test]
		public void creating_a_user_with_empty_password_throws() {
			Assert.Throws<ArgumentNullException>(() =>
				_manager.CreateUserAsync("ouro", "ouro", new[] {"foo", "bar"}, ""));
		}

		[Test]
		public void creating_a_user_with_parameters_can_be_read() {
			UserDetails d = null;
			_manager.CreateUserAsync("ouro", "ourofull", new[] {"foo", "bar"}, "ouro",
				new UserCredentials("admin", "changeit")).Wait();
			Assert.DoesNotThrow(() => {
				d = _manager.GetUserAsync("ouro", new UserCredentials("admin", "changeit")).Result;
			});
			Assert.AreEqual("ouro", d.LoginName);
			Assert.AreEqual("ourofull", d.FullName);
			Assert.AreEqual("foo", d.Groups[0]);
			Assert.AreEqual("bar", d.Groups[1]);
		}
	}
}
