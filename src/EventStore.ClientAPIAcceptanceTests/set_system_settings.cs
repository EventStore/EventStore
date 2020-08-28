using System;
using System.Threading.Tasks;
using EventStore.ClientAPI.Exceptions;
using EventStore.Core.Services;
using EventStore.Core.TransactionLog.Services;
using Xunit;

namespace EventStore.ClientAPI.Tests {
	public class set_system_settings : EventStoreClientAPITest, IAsyncLifetime {
		private readonly EventStoreClientAPIFixture _fixture;

		public set_system_settings(EventStoreClientAPIFixture fixture) {
			_fixture = fixture;
		}

		[Theory, MemberData(nameof(UseSslTestCases))]
		public async Task writes_to_the_correct_stream(bool useSsl) {
			var connection = _fixture.Connections[useSsl];
			var expected = new SystemSettings(
				new StreamAcl(
					Guid.NewGuid().ToString(),
					Guid.NewGuid().ToString(),
					Guid.NewGuid().ToString(),
					Guid.NewGuid().ToString(),
					Guid.NewGuid().ToString()),
				new StreamAcl(
					Guid.NewGuid().ToString(),
					Guid.NewGuid().ToString(),
					Guid.NewGuid().ToString(),
					Guid.NewGuid().ToString(),
					Guid.NewGuid().ToString()));

			await connection.SetSystemSettingsAsync(expected, DefaultUserCredentials.Admin).WithTimeout();

			var result = await connection.ReadStreamEventsBackwardAsync(SystemStreams.SettingsStream, -1, 1,
				false, DefaultUserCredentials.Admin).WithTimeout();

			Assert.Equal(SliceReadStatus.Success, result.Status);

			Assert.Equal(expected.ToJsonBytes(), result.Events[0].OriginalEvent.Data);
		}

		[Theory, MemberData(nameof(UseSslTestCases))]
		public async Task without_permission_throws(bool useSsl) {
			var connection = _fixture.Connections[useSsl];
			await Assert.ThrowsAsync<AccessDeniedException>(() => connection.SetSystemSettingsAsync(new SystemSettings(
				new StreamAcl(
					Guid.NewGuid().ToString(),
					Guid.NewGuid().ToString(),
					Guid.NewGuid().ToString(),
					Guid.NewGuid().ToString(),
					Guid.NewGuid().ToString()),
				new StreamAcl(
					Guid.NewGuid().ToString(),
					Guid.NewGuid().ToString(),
					Guid.NewGuid().ToString(),
					Guid.NewGuid().ToString(),
					Guid.NewGuid().ToString()))).WithTimeout());
		}

		public Task InitializeAsync() => Task.CompletedTask;

		public async Task DisposeAsync() {
			var connection = _fixture.Connections[false];;

			await connection.SetSystemSettingsAsync(new SystemSettings(null, null), DefaultUserCredentials.Admin)
				.WithTimeout();
		}
	}
}
