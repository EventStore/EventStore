using System;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.Client.Users {
	public class deleting_a_user : IClassFixture<deleting_a_user.Fixture> {
		private readonly Fixture _fixture;

		public deleting_a_user(Fixture fixture) {
			_fixture = fixture;
		}

		[Fact]
		public async Task with_null_input_throws() {
			var ex = await Assert.ThrowsAsync<ArgumentNullException>(
				() => _fixture.Client.UsersManager.DeleteUserAsync(null,
					TestCredentials.Root));
			Assert.Equal("loginName", ex.ParamName);
		}

		[Fact]
		public async Task with_empty_input_throws() {
			var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
				() => _fixture.Client.UsersManager.DeleteUserAsync(string.Empty,
					TestCredentials.Root));
			Assert.Equal("loginName", ex.ParamName);
		}

		[Theory, ClassData(typeof(InvalidCredentialsCases))]
		public async Task with_user_with_insufficient_credentials_throws(string loginName,
			UserCredentials userCredentials) {
			await _fixture.Client.UsersManager.CreateUserAsync(loginName, "Full Name", new[] {"foo", "bar"},
				"password", TestCredentials.Root);
			await Assert.ThrowsAsync<NotAuthenticatedException>(
				() => _fixture.Client.UsersManager.DeleteUserAsync(loginName, userCredentials));
		}

		[Fact]
		public async Task cannot_be_read() {
			var loginName = Guid.NewGuid().ToString();
			await _fixture.Client.UsersManager.CreateUserAsync(loginName, "Full Name", new[] {"foo", "bar"}, "password",
				TestCredentials.Root);

			await _fixture.Client.UsersManager.DeleteUserAsync(loginName, TestCredentials.Root);

			var ex = await Assert.ThrowsAsync<UserNotFoundException>(
				() => _fixture.Client.UsersManager.GetUserAsync(loginName, TestCredentials.Root));

			Assert.Equal(loginName, ex.LoginName);
		}

		[Fact]
		public async Task a_second_time_throws() {
			var loginName = Guid.NewGuid().ToString();
			await _fixture.Client.UsersManager.CreateUserAsync(loginName, "Full Name", new[] {"foo", "bar"}, "password",
				TestCredentials.Root);

			await _fixture.Client.UsersManager.DeleteUserAsync(loginName, TestCredentials.Root);

			var ex = await Assert.ThrowsAsync<UserNotFoundException>(
				() => _fixture.Client.UsersManager.DeleteUserAsync(loginName, TestCredentials.Root));

			Assert.Equal(loginName, ex.LoginName);
		}

		public class Fixture : EventStoreGrpcFixture {
			protected override Task Given() => Task.CompletedTask;
			protected override Task When() => Task.CompletedTask;
		}
	}
}
