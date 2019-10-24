using System.Collections.Generic;
using System.Threading.Tasks;
using EventStore.ClientAPI.Exceptions;
using Xunit;

namespace EventStore.ClientAPI.Tests {
	public class delete_stream : EventStoreClientAPITest {
		private readonly EventStoreClientAPIFixture _fixture;

		public delete_stream(EventStoreClientAPIFixture fixture) {
			_fixture = fixture;
		}

		private static IEnumerable<bool> HardDelete => new[] {true, false};

		public static IEnumerable<object[]> HardDeleteCases() {
			foreach (var useSsl in UseSsl)
			foreach (var hardDelete in HardDelete) {
				yield return new object[] {useSsl, hardDelete};
			}
		}

		[Theory, MemberData(nameof(ExpectedVersionTestCases))]
		public async Task that_does_not_exist_with_expected_version_succeeds(long expectedVersion, string displayName,
			bool useSsl) {
			var streamName = $"{GetStreamName()}_{displayName}_{useSsl}";
			using var connection = _fixture.CreateConnection(settings => settings.UseSsl(useSsl));
			await connection.ConnectAsync().WithTimeout();
			await connection.DeleteStreamAsync(streamName, expectedVersion).WithTimeout();
		}

		[Theory, MemberData(nameof(HardDeleteCases))]
		public async Task that_does_not_exist_with_wrong_expected_version_fails(bool useSsl, bool hardDelete) {
			var streamName = $"{GetStreamName()}_{useSsl}_{hardDelete}";
			using var connection = _fixture.CreateConnection(settings => settings.UseSsl(useSsl));
			await connection.ConnectAsync().WithTimeout();

			var ex = await Assert.ThrowsAsync<WrongExpectedVersionException>(
				() => connection.DeleteStreamAsync(streamName, 7, hardDelete).WithTimeout());

			//Assert.Equal(7, ex.ExpectedVersion); TODO JPB looks like a bug
			//Assert.Equal(ExpectedVersion.NoStream, ex.ActualVersion);
		}

		[Theory, MemberData(nameof(HardDeleteCases))]
		public async Task that_does_exist_succeeds(bool useSsl, bool hardDelete) {
			var streamName = $"{GetStreamName()}_{useSsl}_{hardDelete}";
			using var connection = _fixture.CreateConnection(settings => settings.UseSsl(useSsl));
			await connection.ConnectAsync().WithTimeout();

			var result = await connection
				.AppendToStreamAsync(streamName, ExpectedVersion.NoStream, _fixture.CreateTestEvents()).WithTimeout();

			await connection.DeleteStreamAsync(streamName, result.NextExpectedVersion, hardDelete).WithTimeout();
		}
	}
}
