using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.ClientAPI.Tests {
	public class read_event : EventStoreClientAPITest {
		private readonly EventStoreClientAPIFixture _fixture;

		public read_event(EventStoreClientAPIFixture fixture) {
			_fixture = fixture;
		}

		public static IEnumerable<object[]> StreamNameCases() {
			foreach (var sslType in SslTypes) {
				yield return new object[] {string.Empty, sslType};
				yield return new object[] {default(string), sslType};
			}
		}

		[Theory, MemberData(nameof(StreamNameCases))]
		public async Task with_invalid_stream_name_throws(string streamName, SslType sslType) {
			var connection = _fixture.Connections[sslType];

			await Assert.ThrowsAsync<ArgumentNullException>(() =>
				connection.ReadEventAsync(streamName, 0L, false).WithTimeout());
		}

		[Theory, MemberData(nameof(UseSslTestCases))]
		public async Task with_invalid_event_number_throws(SslType sslType) {
			var streamName = GetStreamName();
			var connection = _fixture.Connections[sslType];

			var ex = await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
				() => connection.ReadEventAsync(streamName, -2, false).WithTimeout());
			Assert.Equal("eventNumber", ex.ParamName);
		}

		public static IEnumerable<object[]> NoStreamCases() {
			foreach (var sslType in SslTypes) {
				yield return new object[] {5L, sslType};
				yield return new object[] {-1L, sslType};
			}
		}

		[Theory, MemberData(nameof(NoStreamCases))]
		public async Task for_stream_that_does_not_exist(long expectedVersion, SslType sslType) {
			var streamName = $"{GetStreamName()}_{expectedVersion}_{sslType}";
			var connection = _fixture.Connections[sslType];

			var result = await connection.ReadEventAsync(streamName, expectedVersion, false).WithTimeout();

			Assert.Equal(EventReadStatus.NoStream, result.Status);
			Assert.Null(result.Event);
			Assert.Equal(streamName, result.Stream);
			Assert.Equal(expectedVersion, result.EventNumber);
		}

		[Theory, MemberData(nameof(UseSslTestCases))]
		public async Task for_stream_that_was_deleted(SslType sslType) {
			var streamName = $"{GetStreamName()}_{sslType}";
			var connection = _fixture.Connections[sslType];

			await connection.DeleteStreamAsync(streamName, ExpectedVersion.NoStream, true).WithTimeout();

			var result = await connection.ReadEventAsync(streamName, 5, false).WithTimeout();

			Assert.Equal(EventReadStatus.StreamDeleted, result.Status);
			Assert.Null(result.Event);
			Assert.Equal(streamName, result.Stream);
			Assert.Equal(5, result.EventNumber);
		}

		[Theory, MemberData(nameof(UseSslTestCases))]
		public async Task for_stream_that_exists_but_event_does_not(SslType sslType) {
			var streamName = $"{GetStreamName()}_{sslType}";
			var connection = _fixture.Connections[sslType];

			await connection.AppendToStreamAsync(streamName, ExpectedVersion.NoStream, _fixture.CreateTestEvents(2))
				.WithTimeout();

			var result = await connection.ReadEventAsync(streamName, 5, false).WithTimeout();

			Assert.Equal(EventReadStatus.NotFound, result.Status);
			Assert.Null(result.Event);
			Assert.Equal(streamName, result.Stream);
			Assert.Equal(5, result.EventNumber);
		}

		public static IEnumerable<object[]> StreamExistsCases() {
			foreach (var sslType in SslTypes) {
				yield return new object[] {0, sslType};
				yield return new object[] {1, sslType};
			}
		}

		[Theory, MemberData(nameof(StreamExistsCases))]
		public async Task for_stream_that_exists(int expectedVersion, SslType sslType) {
			var streamName = $"{GetStreamName()}_{expectedVersion}_{sslType}";
			var connection = _fixture.Connections[sslType];

			var testEvents = _fixture.CreateTestEvents(2).ToArray();
			await connection.AppendToStreamAsync(streamName, ExpectedVersion.NoStream, testEvents).WithTimeout();

			var result = await connection.ReadEventAsync(streamName, expectedVersion, false).WithTimeout();
			var expected = testEvents.Skip(expectedVersion).Take(1).Single();

			Assert.Equal(EventReadStatus.Success, result.Status);
			Assert.True(result.Event.HasValue);
			Assert.Equal(expected.EventId, result.Event.Value.OriginalEvent.EventId);
			Assert.Equal(streamName, result.Stream);
			Assert.Equal(expectedVersion, result.EventNumber);
			Assert.Equal(expected.IsJson, result.Event.Value.OriginalEvent.IsJson);
			Assert.NotEqual(default, result.Event.Value.OriginalEvent.Created);
			Assert.NotEqual(default, result.Event.Value.OriginalEvent.CreatedEpoch);
		}

		[Theory, MemberData(nameof(UseSslTestCases))]
		public async Task last_event(SslType sslType) {
			var streamName = $"{GetStreamName()}_{sslType}";
			var connection = _fixture.Connections[sslType];

			var testEvents = _fixture.CreateTestEvents(5).ToArray();
			await connection.AppendToStreamAsync(streamName, ExpectedVersion.NoStream, testEvents).WithTimeout();

			var result = await connection.ReadEventAsync(streamName, -1, false).WithTimeout();
			var expected = testEvents[^1];

			Assert.Equal(EventReadStatus.Success, result.Status);
			Assert.True(result.Event.HasValue);
			Assert.Equal(expected.EventId, result.Event.Value.OriginalEvent.EventId);
			Assert.Equal(streamName, result.Stream);
			Assert.Equal(-1, result.EventNumber);
			Assert.Equal(expected.IsJson, result.Event.Value.OriginalEvent.IsJson);
			Assert.NotEqual(default, result.Event.Value.OriginalEvent.Created);
			Assert.NotEqual(default, result.Event.Value.OriginalEvent.CreatedEpoch);
		}
	}
}
