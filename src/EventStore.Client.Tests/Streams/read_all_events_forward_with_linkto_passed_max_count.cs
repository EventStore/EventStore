using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EventStore.Core.Services;
using Xunit;

namespace EventStore.Client.Streams {
	[Trait("Category", "LongRunning")]
	public class
		read_all_events_forward_with_linkto_passed_max_count : IClassFixture<
			read_all_events_forward_with_linkto_passed_max_count.Fixture> {
		private readonly Fixture _fixture;

		public read_all_events_forward_with_linkto_passed_max_count(Fixture fixture) {
			_fixture = fixture;
		}

		[Fact]
		public void one_event_is_read() {
			Assert.Single(_fixture.Events);
		}

		public class Fixture : EventStoreGrpcFixture {
			private const string DeletedStream = nameof(DeletedStream);
			private const string LinkedStream = nameof(LinkedStream);
			public ResolvedEvent[] Events { get; private set; }

			protected override async Task Given() {
				await Client.AppendToStreamAsync(DeletedStream, AnyStreamRevision.Any, CreateTestEvents());
				await Client.SetStreamMetadataAsync(DeletedStream, AnyStreamRevision.Any,
					new StreamMetadata(maxCount: 2));
				await Client.AppendToStreamAsync(DeletedStream, AnyStreamRevision.Any, CreateTestEvents());
				await Client.AppendToStreamAsync(DeletedStream, AnyStreamRevision.Any, CreateTestEvents());
				await Client.AppendToStreamAsync(LinkedStream, AnyStreamRevision.Any, new[] {
					new EventData(
						Uuid.NewUuid(), SystemEventTypes.LinkTo, Encoding.UTF8.GetBytes("0@" + DeletedStream),
						Array.Empty<byte>(), false)
				});
			}

			protected override async Task When() {
				Events = await Client.ReadStreamAsync(Direction.Forwards, LinkedStream, StreamRevision.Start,
						int.MaxValue, resolveLinkTos: true)
					.ToArrayAsync();
			}
		}
	}
}
