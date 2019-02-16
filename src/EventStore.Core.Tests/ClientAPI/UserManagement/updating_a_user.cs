using System;
using System.Net;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.SystemData;
using EventStore.ClientAPI.UserManagement;
using NUnit.Framework;
using HttpStatusCode = EventStore.ClientAPI.Transport.Http.HttpStatusCode;

namespace EventStore.Core.Tests.ClientAPI.UserManagement {
	[TestFixture, Category("ClientAPI"), Category("LongRunning")]
	public class updating_a_user : TestWithNode {
		[Test]
		public void updating_a_user_with_null_username_throws() {
			Assert.Throws<ArgumentNullException>(() =>
				_manager.UpdateUserAsync(null, "greg", new[] {"foo", "bar"}, new UserCredentials("admin", "changeit")));
		}

		[Test]
		public void updating_a_user_with_empty_username_throws() {
			Assert.Throws<ArgumentNullException>(() =>
				_manager.UpdateUserAsync("", "greg", new[] {"foo", "bar"}, new UserCredentials("admin", "changeit")));
		}

		[Test]
		public void updating_a_user_with_null_name_throws() {
			Assert.Throws<ArgumentNullException>(() =>
				_manager.UpdateUserAsync("greg", null, new[] {"foo", "bar"}, new UserCredentials("admin", "changeit")));
		}

		[Test]
		public void updating_a_user_with_empty_name_throws() {
			Assert.Throws<ArgumentNullException>(() =>
				_manager.UpdateUserAsync("greg", "", new[] {"foo", "bar"}, new UserCredentials("admin", "changeit")));
		}

		[Test]
		public void updating_non_existing_user_throws() {
			Assert.Throws<AggregateException>(() => _manager.UpdateUserAsync(Guid.NewGuid().ToString(), "bar",
				new[] {"foo"}, new UserCredentials("admin", "changeit")).Wait());
		}

		[Test]
		public void updating_a_user_with_parameters_can_be_read() {
			UserDetails d = null;
			_manager.CreateUserAsync("ouro", "ourofull", new[] {"foo", "bar"}, "password",
				new UserCredentials("admin", "changeit")).Wait();
			_manager.UpdateUserAsync("ouro", "something", new[] {"bar", "baz"},
					new UserCredentials("admin", "changeit"))
				.Wait();
			Assert.DoesNotThrow(() => {
				d = _manager.GetUserAsync("ouro", new UserCredentials("admin", "changeit")).Result;
			});
			Assert.AreEqual("ouro", d.LoginName);
			Assert.AreEqual("something", d.FullName);
			Assert.AreEqual("bar", d.Groups[0]);
			Assert.AreEqual("baz", d.Groups[1]);
		}
	}
}
