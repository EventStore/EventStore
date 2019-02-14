using System;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.SystemData;
using EventStore.ClientAPI.Transport.Http;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.UserManagement {
	[TestFixture, Category("ClientAPI"), Category("LongRunning")]
	public class deleting_a_user : TestWithNode {
		[Test]
		public void deleting_non_existing_user_throws() {
			var ex = Assert.Throws<AggregateException>(() =>
				_manager.DeleteUserAsync(Guid.NewGuid().ToString(), new UserCredentials("admin", "changeit")).Wait());
			var realex = (UserCommandFailedException)ex.InnerException;
			Assert.AreEqual(HttpStatusCode.NotFound, realex.HttpStatusCode);
		}

		[Test]
		public void deleting_created_user_deletes_it() {
			var user = Guid.NewGuid().ToString();
			Assert.DoesNotThrow(() => _manager.CreateUserAsync(user, "ourofull", new[] {"foo", "bar"}, "ouro",
				new UserCredentials("admin", "changeit")).Wait());
			Assert.DoesNotThrow(() => _manager.DeleteUserAsync(user, new UserCredentials("admin", "changeit")).Wait());
		}


		[Test]
		public void deleting_null_user_throws() {
			Assert.Throws<ArgumentNullException>(() =>
				_manager.DeleteUserAsync(null, new UserCredentials("admin", "changeit")));
		}

		[Test]
		public void deleting_empty_user_throws() {
			Assert.Throws<ArgumentNullException>(() =>
				_manager.DeleteUserAsync("", new UserCredentials("admin", "changeit")));
		}

		[Test]
		public void can_delete_a_user() {
			_manager.CreateUserAsync("ouro", "ouro", new[] {"foo", "bar"}, "ouro",
				new UserCredentials("admin", "changeit")).Wait();
			Assert.DoesNotThrow(() => {
				var x = _manager.GetUserAsync("ouro", new UserCredentials("admin", "changeit")).Result;
			});
			_manager.DeleteUserAsync("ouro", new UserCredentials("admin", "changeit")).Wait();

			var ex = Assert.Throws<AggregateException>(
				() => {
					var x = _manager.GetUserAsync("ouro", new UserCredentials("admin", "changeit")).Result;
				}
			);
			Assert.AreEqual(HttpStatusCode.NotFound,
				((UserCommandFailedException)ex.InnerException.InnerException).HttpStatusCode);
		}
	}
}
