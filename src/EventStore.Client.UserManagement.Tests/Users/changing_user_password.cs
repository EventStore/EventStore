using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.Client.Users {
	public class changing_user_password : IClassFixture<changing_user_password.Fixture> {
		private readonly Fixture _fixture;

		public changing_user_password(Fixture fixture) {
			_fixture = fixture;
		}

		public static IEnumerable<object[]> NullInputCases() {
			var loginName = "ouro";
			var currentPassword = "foofoofoo";
			var newPassword = "foofoofoofoofoofoo";

			yield return new object[] {null, currentPassword, newPassword, nameof(loginName)};
			yield return new object[] {loginName, null, newPassword, nameof(currentPassword)};
			yield return new object[] {loginName, currentPassword, null, nameof(newPassword)};
		}

		[Theory, MemberData(nameof(NullInputCases))]
		public async Task with_null_input_throws(string loginName, string currentPassword, string newPassword,
			string paramName) {
			var ex = await Assert.ThrowsAsync<ArgumentNullException>(
				() => _fixture.Client.UsersManager.ChangePasswordAsync(loginName, currentPassword, newPassword,
					TestCredentials.Root));
			Assert.Equal(paramName, ex.ParamName);
		}

		public static IEnumerable<object[]> EmptyInputCases() {
			var loginName = "ouro";
			var currentPassword = "foofoofoo";
			var newPassword = "foofoofoofoofoofoo";

			yield return new object[] {string.Empty, currentPassword, newPassword, nameof(loginName)};
			yield return new object[] {loginName, string.Empty, newPassword, nameof(currentPassword)};
			yield return new object[] {loginName, currentPassword, string.Empty, nameof(newPassword)};
		}

		[Theory, MemberData(nameof(EmptyInputCases))]
		public async Task with_empty_input_throws(string loginName, string currentPassword, string newPassword,
			string paramName) {
			var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
				() => _fixture.Client.UsersManager.ChangePasswordAsync(loginName, currentPassword, newPassword,
					TestCredentials.Root));
			Assert.Equal(paramName, ex.ParamName);
		}

		[Theory(Skip = "This can't be right"), ClassData(typeof(InsufficientCredentialsCases))]
		public async Task with_user_with_insufficient_credentials_throws(string loginName,
			UserCredentials userCredentials) {
			await _fixture.Client.UsersManager.CreateUserAsync(loginName, "Full Name", Array.Empty<string>(),
				"password", TestCredentials.Root);
			await Assert.ThrowsAsync<AccessDeniedException>(
				() => _fixture.Client.UsersManager.ChangePasswordAsync(loginName, "password", "newPassword",
					userCredentials));
		}

		[Fact]
		public async Task when_the_current_password_is_wrong_throws() {
			var loginName = Guid.NewGuid().ToString();
			await _fixture.Client.UsersManager.CreateUserAsync(loginName, "Full Name", Array.Empty<string>(),
				"password", TestCredentials.Root);
			await Assert.ThrowsAsync<AccessDeniedException>(
				() => _fixture.Client.UsersManager.ChangePasswordAsync(loginName, "wrong-password", "newPassword",
					TestCredentials.Root));
		}

		[Fact]
		public async Task with_correct_credentials() {
			var loginName = Guid.NewGuid().ToString();
			await _fixture.Client.UsersManager.CreateUserAsync(loginName, "Full Name", Array.Empty<string>(),
				"password", TestCredentials.Root);

			await _fixture.Client.UsersManager.ChangePasswordAsync(loginName, "password", "newPassword",
				TestCredentials.Root);
		}

		[Fact]
		public async Task with_own_credentials() {
			var loginName = Guid.NewGuid().ToString();
			await _fixture.Client.UsersManager.CreateUserAsync(loginName, "Full Name", Array.Empty<string>(),
				"password", TestCredentials.Root);

			await _fixture.Client.UsersManager.ChangePasswordAsync(loginName, "password", "newPassword",
				new UserCredentials(loginName, "password"));
		}

		public class Fixture : EventStoreGrpcFixture {
			protected override Task Given() => Task.CompletedTask;
			protected override Task When() => Task.CompletedTask;
		}
	}
}
