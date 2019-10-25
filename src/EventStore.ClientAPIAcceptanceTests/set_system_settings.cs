using System;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Exceptions;
using EventStore.Core.Services;
using Xunit;
using StreamAcl = EventStore.ClientAPI.StreamAcl;

namespace EventStore.ClientAPI.Tests {
	[Collection(nameof(EventStoreClientAPIFixture))]
	public class set_system_settings : EventStoreClientAPITest, IClassFixture<EventStoreClientAPIFixture> {
		private readonly EventStoreClientAPIFixture _fixture;

		public set_system_settings(EventStoreClientAPIFixture fixture) {
			_fixture = fixture;
		}

		[Theory, MemberData(nameof(UseSslTestCases))]
		public async Task writes_to_the_correct_stream(bool useSsl) {
			using var connection = _fixture.CreateConnection(settings => settings.UseSsl(useSsl));
			await connection.ConnectAsync();

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

			await connection.SetSystemSettingsAsync(expected, DefaultUserCredentials.Admin);

			var result = await connection.ReadStreamEventsBackwardAsync(SystemStreams.SettingsStream, -1, 1,
				false, DefaultUserCredentials.Admin);
			
			Assert.Equal(SliceReadStatus.Success, result.Status);
			
			Assert.Equal(expected.ToJsonBytes(), result.Events[0].OriginalEvent.Data);
		}

		[Theory, MemberData(nameof(UseSslTestCases))]
		public async Task without_permission_throws(bool useSsl) {
			using var connection = _fixture.CreateConnection(settings => settings.UseSsl(useSsl));
			await connection.ConnectAsync();

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
					Guid.NewGuid().ToString()))));
		}
	}
}
