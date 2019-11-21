﻿using System;
using System.Threading.Tasks;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.SystemData;
using EventStore.ClientAPI.Transport.Http;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.UserManagement {
	[TestFixture, Category("ClientAPI"), Category("LongRunning")]
	public class deleting_a_user : TestWithNode {
		[Test]
		public async Task deleting_non_existing_user_throws() {
			var ex = await AssertEx.ThrowsAsync<UserCommandFailedException>(() =>
				_manager.DeleteUserAsync(Guid.NewGuid().ToString(), new UserCredentials("admin", "changeit")));
			Assert.AreEqual(HttpStatusCode.NotFound, ex.HttpStatusCode);
		}

		[Test]
		public async Task deleting_created_user_deletes_it() {
			var user = Guid.NewGuid().ToString();
			await _manager.CreateUserAsync(user, "ourofull", new[] { "foo", "bar" }, "ouro",
				new UserCredentials("admin", "changeit"));
			await _manager.DeleteUserAsync(user, new UserCredentials("admin", "changeit"));
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
		public async Task can_delete_a_user() {
			await _manager.CreateUserAsync("ouro", "ouro", new[] { "foo", "bar" }, "ouro",
				new UserCredentials("admin", "changeit"));
			var x = await _manager.GetUserAsync("ouro", new UserCredentials("admin", "changeit"));
			await _manager.DeleteUserAsync("ouro", new UserCredentials("admin", "changeit"));

			var ex = await AssertEx.ThrowsAsync<AggregateException>(
				() => _manager.GetUserAsync("ouro", new UserCredentials("admin", "changeit")));
			Assert.AreEqual(HttpStatusCode.NotFound,
				((UserCommandFailedException)ex.InnerException).HttpStatusCode);
		}
	}
}
