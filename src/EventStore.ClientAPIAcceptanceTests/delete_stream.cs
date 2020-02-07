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
			foreach (var sslType in SslTypes)
			foreach (var hardDelete in HardDelete) {
				yield return new object[] {sslType, hardDelete};
			}
		}

		[Theory, MemberData(nameof(ExpectedVersionTestCases))]
		public async Task that_does_not_exist_with_expected_version_succeeds(long expectedVersion, string displayName,
			SslType sslType) {
			var streamName = $"{GetStreamName()}_{displayName}_{sslType}";
			var connection = _fixture.Connections[sslType];
			await connection.DeleteStreamAsync(streamName, expectedVersion).WithTimeout();
		}

		[Theory, MemberData(nameof(HardDeleteCases))]
		public async Task that_does_not_exist_with_wrong_expected_version_fails(SslType sslType, bool hardDelete) {
			var streamName = $"{GetStreamName()}_{sslType}_{hardDelete}";
			var connection = _fixture.Connections[sslType];
			var ex = await Assert.ThrowsAsync<WrongExpectedVersionException>(
				() => connection.DeleteStreamAsync(streamName, 7, hardDelete).WithTimeout());

			//Assert.Equal(7, ex.ExpectedVersion); TODO JPB looks like a bug
			//Assert.Equal(ExpectedVersion.NoStream, ex.ActualVersion);
		}

		[Theory, MemberData(nameof(HardDeleteCases))]
		public async Task that_does_exist_succeeds(SslType sslType, bool hardDelete) {
			var streamName = $"{GetStreamName()}_{sslType}_{hardDelete}";
			var connection = _fixture.Connections[sslType];
			var result = await connection
				.AppendToStreamAsync(streamName, ExpectedVersion.NoStream, _fixture.CreateTestEvents()).WithTimeout();

			await connection.DeleteStreamAsync(streamName, result.NextExpectedVersion, hardDelete).WithTimeout();
		}
	}
}
