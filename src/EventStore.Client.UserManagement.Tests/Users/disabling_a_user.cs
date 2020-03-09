using System;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.Client.Users {
	public class disabling_a_user : IClassFixture<disabling_a_user.Fixture> {
		private readonly Fixture _fixture;

		public disabling_a_user(Fixture fixture) {
			_fixture = fixture;
		}

		[Fact]
		public async Task with_null_input_throws() {
			var ex = await Assert.ThrowsAsync<ArgumentNullException>(
				() => _fixture.Client.UsersManager.EnableUserAsync(null,
					TestCredentials.Root));
			Assert.Equal("loginName", ex.ParamName);
		}

		[Fact]
		public async Task with_empty_input_throws() {
			var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
				() => _fixture.Client.UsersManager.EnableUserAsync(string.Empty,
					TestCredentials.Root));
			Assert.Equal("loginName", ex.ParamName);
		}

		[Theory, ClassData(typeof(InvalidCredentialsCases))]
		public async Task with_user_with_insufficient_credentials_throws(string loginName,
			UserCredentials userCredentials) {
			await _fixture.Client.UsersManager.CreateUserAsync(loginName, "Full Name", new[] {"foo", "bar"},
				"password", TestCredentials.Root);
			if (userCredentials == null)
				await Assert.ThrowsAsync<AccessDeniedException>(() => _fixture.Client.UsersManager.DisableUserAsync(loginName, userCredentials));
			else
				await Assert.ThrowsAsync<NotAuthenticatedException>(
					() => _fixture.Client.UsersManager.DisableUserAsync(loginName, userCredentials));
		}

		[Fact]
		public async Task that_was_disabled() {
			var loginName = Guid.NewGuid().ToString();
			await _fixture.Client.UsersManager.CreateUserAsync(loginName, "Full Name", new[] {"foo", "bar"},
				"password", TestCredentials.Root);

			await _fixture.Client.UsersManager.DisableUserAsync(loginName, TestCredentials.Root);
			await _fixture.Client.UsersManager.DisableUserAsync(loginName, TestCredentials.Root);
		}

		[Fact]
		public async Task that_is_enabled() {
			var loginName = Guid.NewGuid().ToString();
			await _fixture.Client.UsersManager.CreateUserAsync(loginName, "Full Name", new[] {"foo", "bar"},
				"password", TestCredentials.Root);

			await _fixture.Client.UsersManager.DisableUserAsync(loginName, TestCredentials.Root);
		}

		public class Fixture : EventStoreGrpcFixture {
			protected override Task Given() => Task.CompletedTask;
			protected override Task When() => Task.CompletedTask;
		}
	}
}
