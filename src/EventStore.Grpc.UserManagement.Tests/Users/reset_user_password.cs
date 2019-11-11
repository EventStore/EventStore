using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.Grpc.Users {
	public class resetting_user_password : IClassFixture<resetting_user_password.Fixture> {
		private readonly Fixture _fixture;

		public resetting_user_password(Fixture fixture) {
			_fixture = fixture;
		}

		public static IEnumerable<object[]> NullInputCases() {
			var loginName = "ouro";
			var newPassword = "foofoofoofoofoofoo";

			yield return new object[] {null, newPassword, nameof(loginName)};
			yield return new object[] {loginName, null, nameof(newPassword)};
		}

		[Theory, MemberData(nameof(NullInputCases))]
		public async Task with_null_input_throws(string loginName, string newPassword,
			string paramName) {
			var ex = await Assert.ThrowsAsync<ArgumentNullException>(
				() => _fixture.Client.UsersManager.ResetPasswordAsync(loginName, newPassword,
					TestCredentials.Root));
			Assert.Equal(paramName, ex.ParamName);
		}

		public static IEnumerable<object[]> EmptyInputCases() {
			var loginName = "ouro";
			var newPassword = "foofoofoofoofoofoo";

			yield return new object[] {string.Empty, newPassword, nameof(loginName)};
			yield return new object[] {loginName, string.Empty, nameof(newPassword)};
		}

		[Theory, MemberData(nameof(EmptyInputCases))]
		public async Task with_empty_input_throws(string loginName, string newPassword,
			string paramName) {
			var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
				() => _fixture.Client.UsersManager.ResetPasswordAsync(loginName, newPassword,
					TestCredentials.Root));
			Assert.Equal(paramName, ex.ParamName);
		}

		[Theory, ClassData(typeof(InsufficientCredentialsCases))]
		public async Task with_user_with_insufficient_credentials_throws(string loginName,
			UserCredentials userCredentials) {
			await _fixture.Client.UsersManager.CreateUserAsync(loginName, "Full Name", Array.Empty<string>(),
				"password", TestCredentials.Root);
			await Assert.ThrowsAsync<AccessDeniedException>(
				() => _fixture.Client.UsersManager.ResetPasswordAsync(loginName, "newPassword",
					userCredentials));
		}

		[Fact]
		public async Task with_correct_credentials() {
			var loginName = Guid.NewGuid().ToString();
			await _fixture.Client.UsersManager.CreateUserAsync(loginName, "Full Name", Array.Empty<string>(),
				"password", TestCredentials.Root);

			await _fixture.Client.UsersManager.ResetPasswordAsync(loginName, "newPassword",
				TestCredentials.Root);
		}

		[Fact]
		public async Task with_own_credentials_throws() {
			var loginName = Guid.NewGuid().ToString();
			await _fixture.Client.UsersManager.CreateUserAsync(loginName, "Full Name", Array.Empty<string>(),
				"password", TestCredentials.Root);

			await Assert.ThrowsAsync<AccessDeniedException>(
				() => _fixture.Client.UsersManager.ResetPasswordAsync(loginName, "newPassword",
					new UserCredentials(loginName, "password")));
		}

		public class Fixture : EventStoreGrpcFixture {
			protected override Task Given() => Task.CompletedTask;
			protected override Task When() => Task.CompletedTask;
		}
	}
}
