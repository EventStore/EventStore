using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using EventStore.Core.Services;
using Xunit;

namespace EventStore.Client.Streams {
	public abstract class read_events_linked_to_deleted_stream {
		private readonly Fixture _fixture;

		protected read_events_linked_to_deleted_stream(Fixture fixture) {
			_fixture = fixture;
		}

		[Fact]
		public void one_event_is_read() {
			Assert.Single(_fixture.Events);
		}

		[Fact]
		public void the_linked_event_is_not_resolved() {
			Assert.Null(_fixture.Events[0].Event);
		}

		[Fact]
		public void the_link_event_is_included() {
			Assert.NotNull(_fixture.Events[0].OriginalEvent);
		}

		[Fact]
		public void the_event_is_not_resolved() {
			Assert.False(_fixture.Events[0].IsResolved);
		}

		public abstract class Fixture : EventStoreGrpcFixture {
			private const string DeletedStream = nameof(DeletedStream);
			protected const string LinkedStream = nameof(LinkedStream);
			public ResolvedEvent[] Events { get; private set; }

			protected override async Task Given() {
				await Client.AppendToStreamAsync(DeletedStream, AnyStreamRevision.Any, CreateTestEvents());
				await Client.AppendToStreamAsync(LinkedStream, AnyStreamRevision.Any, new[] {
					new EventData(
						Uuid.NewUuid(), SystemEventTypes.LinkTo, Encoding.UTF8.GetBytes("0@" + DeletedStream),
						Array.Empty<byte>(), Constants.Metadata.ContentTypes.ApplicationOctetStream)
				});

				await Client.SoftDeleteAsync(DeletedStream, AnyStreamRevision.Any);
			}

			protected override async Task When() {
				Events = await Read();
			}

			protected abstract ValueTask<ResolvedEvent[]> Read();
		}

		public class forwards : read_events_linked_to_deleted_stream, IClassFixture<forwards.Fixture> {
			public forwards(Fixture fixture) : base(fixture) {
			}

			public new class Fixture : read_events_linked_to_deleted_stream.Fixture {
				protected override ValueTask<ResolvedEvent[]> Read()
					=> Client.ReadStreamAsync(Direction.Forwards, LinkedStream, StreamRevision.Start, 1,
						resolveLinkTos: true).ToArrayAsync();
			}
		}

		public class backwards : read_events_linked_to_deleted_stream, IClassFixture<backwards.Fixture> {
			public backwards(Fixture fixture) : base(fixture) {
			}

			public new class Fixture : read_events_linked_to_deleted_stream.Fixture {
				protected override ValueTask<ResolvedEvent[]> Read()
					=> Client.ReadStreamAsync(Direction.Backwards, LinkedStream, StreamRevision.Start, 1,
						resolveLinkTos: true).ToArrayAsync();
			}
		}
	}
}
