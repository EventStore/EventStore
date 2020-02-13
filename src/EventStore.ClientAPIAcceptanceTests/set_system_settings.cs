using System;
using System.Threading.Tasks;
using EventStore.ClientAPI.Exceptions;
using EventStore.Core.Services;
using Xunit;

namespace EventStore.ClientAPI.Tests {
	public class set_system_settings : EventStoreClientAPITest, IAsyncLifetime {
		private readonly EventStoreClientAPIFixture _fixture;

		public set_system_settings(EventStoreClientAPIFixture fixture) {
			_fixture = fixture;
		}

		[Theory, MemberData(nameof(UseSslTestCases))]
		public async Task writes_to_the_correct_stream(SslType sslType) {
			var connection = _fixture.Connections[sslType];
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
		public async Task without_permission_throws_except_if_using_admin_client_certificate(SslType sslType) {
			var connection = _fixture.Connections[sslType];
			try {
				await connection.SetSystemSettingsAsync(new SystemSettings(
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
						Guid.NewGuid().ToString()))).WithTimeout();
			} catch (AccessDeniedException) {
				Assert.True(sslType != SslType.WithAdminClientCertificate);
				return;
			}

			Assert.True(sslType == SslType.WithAdminClientCertificate);
		}

		public Task InitializeAsync() => Task.CompletedTask;

		public async Task DisposeAsync() {
			var connection = _fixture.Connections[SslType.None];;

			await connection.SetSystemSettingsAsync(new SystemSettings(null, null), DefaultUserCredentials.Admin)
				.WithTimeout();
		}
	}
}
