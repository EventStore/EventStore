using System;
using System.Threading.Tasks;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.SystemData;
using EventStore.ClientAPI.Transport.Http;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.UserManagement {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	public class reset_password<TLogFormat, TStreamId> : TestWithUser<TLogFormat, TStreamId> {
		[Test]
		public async Task null_user_name_throws() {
			await AssertEx.ThrowsAsync<ArgumentNullException>(() =>
				_manager.ResetPasswordAsync(null, "foo", new UserCredentials("admin", "changeit")));
		}

		[Test]
		public async Task empty_user_name_throws() {
			await AssertEx.ThrowsAsync<ArgumentNullException>(() =>
				_manager.ResetPasswordAsync("", "foo", new UserCredentials("admin", "changeit")));
		}

		[Test]
		public async Task empty_password_throws() {
			await AssertEx.ThrowsAsync<ArgumentNullException>(() =>
				_manager.ResetPasswordAsync(_username, "", new UserCredentials("admin", "changeit")));
		}

		[Test]
		public async Task null_password_throws() {
			await AssertEx.ThrowsAsync<ArgumentNullException>(() =>
				_manager.ResetPasswordAsync(_username, null, new UserCredentials("admin", "changeit")));
		}

		[Test]
		public async Task can_reset_password() {
			await _manager.ResetPasswordAsync(_username, "foo", new UserCredentials("admin", "changeit"));
			var ex = await AssertEx.ThrowsAsync<UserCommandFailedException>(
				() => _manager.ChangePasswordAsync(_username, "password", "foobar",
					new UserCredentials(_username, "password"))
			);
			Assert.AreEqual(HttpStatusCode.Unauthorized,
				ex.HttpStatusCode);
		}
	}

	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(long))]
	public class change_password<TLogFormat, TStreamId> : TestWithUser<TLogFormat, TStreamId> {
		[Test]
		public async Task null_username_throws() {
			await AssertEx.ThrowsAsync<ArgumentNullException>(() =>
				_manager.ChangePasswordAsync(null, "oldpassword", "newpassword",
					new UserCredentials("admin", "changeit")));
		}

		[Test]
		public async Task empty_username_throws() {
			await AssertEx.ThrowsAsync<ArgumentNullException>(() =>
				_manager.ChangePasswordAsync("", "oldpassword", "newpassword", new UserCredentials("admin", "changeit"))
					);
		}

		[Test]
		public async Task null_current_password_throws() {
			await AssertEx.ThrowsAsync<ArgumentNullException>(() =>
				_manager.ChangePasswordAsync(_username, null, "newpassword", new UserCredentials("admin", "changeit"))
					);
		}

		[Test]
		public async Task empty_current_password_throws() {
			await AssertEx.ThrowsAsync<ArgumentNullException>(() =>
				_manager.ChangePasswordAsync(_username, "", "newpassword", new UserCredentials("admin", "changeit"))
					);
		}

		[Test]
		public async Task null_new_password_throws() {
			await AssertEx.ThrowsAsync<ArgumentNullException>(() =>
				_manager.ChangePasswordAsync(_username, "oldpasword", null, new UserCredentials("admin", "changeit"))
					);
		}

		[Test]
		public async Task empty_new_password_throws() {
			await AssertEx.ThrowsAsync<ArgumentNullException>(() =>
				_manager.ChangePasswordAsync(_username, "oldpassword", "", new UserCredentials("admin", "changeit"))
					);
		}

		[Test]
		public async Task can_change_password() {
			await _manager.ChangePasswordAsync(_username, "password", "fubar", new UserCredentials(_username, "password"));
			var ex = await AssertEx.ThrowsAsync<UserCommandFailedException>(
				() => _manager.ChangePasswordAsync(_username, "password", "foobar",
					new UserCredentials(_username, "password"))
			);
			Assert.AreEqual(HttpStatusCode.Unauthorized,
				ex.HttpStatusCode);
		}
	}
}
