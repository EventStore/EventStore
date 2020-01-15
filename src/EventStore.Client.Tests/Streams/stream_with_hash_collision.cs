using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.Client.Streams {
	[Trait("Category", "LongRunning")]
	public abstract class stream_with_hash_collision {
		private readonly Fixture _fixture;
		private const string Stream1 = "account--696193173";
		private const string Stream2 = "LPN-FC002_LPK51001";
		protected static string GetPathName() => $"{nameof(stream_with_hash_collision)}_{Guid.NewGuid():n}";

		protected stream_with_hash_collision(Fixture fixture) {
			_fixture = fixture;
		}

		public class Fixture : EventStoreGrpcFixture {
			private readonly DirectoryInfo _directory;
			public bool DeleteDirectory { get; set; }

			public Fixture(string pathName) : base(builder => builder
				.RunOnDisk(pathName)
				.WithHashCollisionReadLimitOf(1)
				.WithIndexBitnessVersion(Core.Index.PTableVersions.IndexV1)) {
				_directory = Directory.CreateDirectory(pathName);
			}

			protected override async Task Given() {
				var writeResult = await Client.AppendToStreamAsync(Stream1, AnyStreamRevision.NoStream,
					CreateTestEvents()).WithTimeout();

				Assert.Equal(0, writeResult.NextExpectedVersion);

				//Write 100 events to stream 2 which will have the same hash as stream 1.
				for (var i = 0; i < 100; i++) {
					writeResult = await Client.AppendToStreamAsync(
						Stream2,
						AnyStreamRevision.Any,
						CreateTestEvents()).WithTimeout();
					Assert.Equal(i, writeResult.NextExpectedVersion);
				}
			}

			protected override Task When() => DisposeAsync();

			public override async Task DisposeAsync() {
				await base.DisposeAsync();

				if (DeleteDirectory) {
					_directory.Delete(true);
				}
			}
		}

		public class forwards : stream_with_hash_collision, IClassFixture<forwards.Fixture> {
			public forwards(Fixture fixture)
				: base(fixture) {
			}

			[Fact]
			public async Task throws_not_found() {
				await using var fixture = new Fixture { DeleteDirectory = true };

				await fixture.Node.StartAsync(true);

				await Assert.ThrowsAsync<StreamNotFoundException>(
					() => fixture.Client.ReadStreamForwardsAsync(Stream1, StreamRevision.Start, 1, true)
						.ToArrayAsync().AsTask());
			}

			public new class Fixture : stream_with_hash_collision.Fixture {
				public Fixture() : base(GetPathName()) {
				}
			}
		}

		public class backwards : stream_with_hash_collision, IClassFixture<backwards.Fixture> {
			public backwards(Fixture fixture)
				: base(fixture) {
			}

			[Fact]
			public async Task throws_not_found() {
				await using var fixture = new Fixture { DeleteDirectory = true };

				await fixture.Node.StartAsync(true);

				await Assert.ThrowsAsync<StreamNotFoundException>(
					() => fixture.Client.ReadStreamBackwardsAsync(Stream1, StreamRevision.End, 1, true)
						.ToArrayAsync().AsTask());
			}

			public new class Fixture : stream_with_hash_collision.Fixture {
				public Fixture() : base(GetPathName()) {
				}
			}
		}
	}
}
