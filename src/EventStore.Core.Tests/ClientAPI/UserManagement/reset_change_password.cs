using System;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.SystemData;
using EventStore.ClientAPI.Transport.Http;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.UserManagement {
	public class reset_password : TestWithUser {
		[Test]
		public void null_user_name_throws() {
			Assert.Throws<ArgumentNullException>(() =>
				_manager.ResetPasswordAsync(null, "foo", new UserCredentials("admin", "changeit")).Wait());
		}

		[Test]
		public void empty_user_name_throws() {
			Assert.Throws<ArgumentNullException>(() =>
				_manager.ResetPasswordAsync("", "foo", new UserCredentials("admin", "changeit")).Wait());
		}

		[Test]
		public void empty_password_throws() {
			Assert.Throws<ArgumentNullException>(() =>
				_manager.ResetPasswordAsync(_username, "", new UserCredentials("admin", "changeit")).Wait());
		}

		[Test]
		public void null_password_throws() {
			Assert.Throws<ArgumentNullException>(() =>
				_manager.ResetPasswordAsync(_username, null, new UserCredentials("admin", "changeit")).Wait());
		}

		[Test]
		public void can_reset_password() {
			_manager.ResetPasswordAsync(_username, "foo", new UserCredentials("admin", "changeit")).Wait();
			var ex = Assert.Throws<AggregateException>(
				() => _manager.ChangePasswordAsync(_username, "password", "foobar",
					new UserCredentials(_username, "password")).Wait()
			);
			Assert.AreEqual(HttpStatusCode.Unauthorized,
				((UserCommandFailedException)ex.InnerException).HttpStatusCode);
		}
	}

	public class change_password : TestWithUser {
		[Test]
		public void null_username_throws() {
			Assert.Throws<ArgumentNullException>(() =>
				_manager.ChangePasswordAsync(null, "oldpassword", "newpassword",
					new UserCredentials("admin", "changeit")).Wait());
		}

		[Test]
		public void empty_username_throws() {
			Assert.Throws<ArgumentNullException>(() =>
				_manager.ChangePasswordAsync("", "oldpassword", "newpassword", new UserCredentials("admin", "changeit"))
					.Wait());
		}

		[Test]
		public void null_current_password_throws() {
			Assert.Throws<ArgumentNullException>(() =>
				_manager.ChangePasswordAsync(_username, null, "newpassword", new UserCredentials("admin", "changeit"))
					.Wait());
		}

		[Test]
		public void empty_current_password_throws() {
			Assert.Throws<ArgumentNullException>(() =>
				_manager.ChangePasswordAsync(_username, "", "newpassword", new UserCredentials("admin", "changeit"))
					.Wait());
		}

		[Test]
		public void null_new_password_throws() {
			Assert.Throws<ArgumentNullException>(() =>
				_manager.ChangePasswordAsync(_username, "oldpasword", null, new UserCredentials("admin", "changeit"))
					.Wait());
		}

		[Test]
		public void empty_new_password_throws() {
			Assert.Throws<ArgumentNullException>(() =>
				_manager.ChangePasswordAsync(_username, "oldpassword", "", new UserCredentials("admin", "changeit"))
					.Wait());
		}

		[Test]
		public void can_change_password() {
			_manager.ChangePasswordAsync(_username, "password", "fubar", new UserCredentials(_username, "password"))
				.Wait();
			var ex = Assert.Throws<AggregateException>(
				() => _manager.ChangePasswordAsync(_username, "password", "foobar",
					new UserCredentials(_username, "password")).Wait()
			);
			Assert.AreEqual(HttpStatusCode.Unauthorized,
				((UserCommandFailedException)ex.InnerException).HttpStatusCode);
		}
	}
}
