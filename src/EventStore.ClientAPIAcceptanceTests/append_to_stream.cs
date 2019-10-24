using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using EventStore.ClientAPI;
using EventStore.ClientAPI.Exceptions;
using Xunit;

namespace EventStore.ClientAPIAcceptanceTests {
	public class append_to_stream : EventStoreClientAPITest, IClassFixture<EventStoreClientAPIFixture> {
		private readonly EventStoreClientAPIFixture _fixture;

		public append_to_stream(EventStoreClientAPIFixture fixture) {
			_fixture = fixture;
		}

		public static IEnumerable<object[]> ExpectedVersionTestCases() {
			yield return new object[] {ExpectedVersion.Any, nameof(ExpectedVersion.Any)};
			yield return new object[] {ExpectedVersion.NoStream, nameof(ExpectedVersion.NoStream)};
		}

		[Theory, MemberData(nameof(ExpectedVersionTestCases))]
		public async Task expected_version(long expectedVersion, string displayName) {
			var streamName = $"{GetStreamName()}_{displayName}";
			using var connection = _fixture.CreateConnection();

			await connection.ConnectAsync();

			var result = await connection.AppendToStreamAsync(streamName, expectedVersion, _fixture.CreateTestEvents());
			Assert.Equal(0, result.NextExpectedVersion);
			await connection.AppendToStreamAsync(streamName, result.NextExpectedVersion, _fixture.CreateTestEvents());
		}
		
		[Fact]
		public async Task wrong_expected_version() {
			var streamName = GetStreamName();
			using var connection = _fixture.CreateConnection();

			await connection.ConnectAsync();

			var ex = await Assert.ThrowsAsync<WrongExpectedVersionException>(() =>
				connection.AppendToStreamAsync(streamName, ExpectedVersion.StreamExists, _fixture.CreateTestEvents()));
			
			Assert.Equal(ExpectedVersion.StreamExists, ex.ExpectedVersion);
			Assert.Equal(ExpectedVersion.NoStream, ex.ActualVersion);
		}

	}
}
