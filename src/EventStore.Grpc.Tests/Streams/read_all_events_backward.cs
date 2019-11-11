using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.Grpc.Streams {
	[Trait("Category", "LongRunning")]
	public class read_all_events_backward : IClassFixture<read_all_events_backward.Fixture> {
		private const string Stream = "stream";
		private readonly Fixture _fixture;

		public read_all_events_backward(Fixture fixture) {
			_fixture = fixture;
		}

		[Fact]
		public async Task return_empty_if_reading_from_start() {
			var count = await _fixture.Client.ReadAllBackwardsAsync(Position.Start, 1, false).CountAsync();
			Assert.Equal(0, count);
		}

		[Fact]
		public async Task return_partial_slice_if_not_enough_events() {
			var events = await _fixture.Client.ReadAllBackwardsAsync(Position.End, _fixture.Events.Length * 2, false)
				.ToArrayAsync();

			Assert.True(events.Length < _fixture.Events.Length * 2);
		}

		[Fact]
		public async Task return_events_in_reversed_order_compared_to_written() {
			var events = await _fixture.Client.ReadAllBackwardsAsync(Position.End, _fixture.Events.Length, false)
				.ToArrayAsync();

			Assert.True(EventDataComparer.Equal(
				_fixture.Events.Reverse().ToArray(),
				events.AsResolvedTestEvents().ToArray()));
		}

		[Fact(Skip = "Not Implemented")]
		public async Task be_able_to_read_all_one_by_one_until_end_of_stream() {
			throw new NotImplementedException();
		}

		[Fact(Skip = "Not Implemented")]
		public async Task be_able_to_read_events_slice_at_time() {
			throw new NotImplementedException();
		}

		[Fact(Skip = "Not Implemented")]
		public async Task when_got_int_max_value_as_maxcount_should_throw() {
			throw new NotImplementedException();
		}

		public class Fixture : EventStoreGrpcFixture {
			public EventData[] Events { get; private set; }

			protected override async Task Given() {
				var result = await Client.SetStreamMetadataAsync(
					"$all",
					AnyStreamRevision.NoStream,
					new StreamMetadata(acl: new StreamAcl(readRole: "$all")),
					TestCredentials.Root);
				Events = CreateTestEvents(20).ToArray();

				await Client.AppendToStreamAsync(Stream, AnyStreamRevision.NoStream, Events);
			}

			protected override Task When() => Task.CompletedTask;
		}
	}
}
