using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.Client.Streams {
	[Trait("Category", "LongRunning")]
	public class append_to_stream_with_hash_collisions
		: IClassFixture<append_to_stream_with_hash_collisions.Fixture> {
		private readonly Fixture _fixture;

		public append_to_stream_with_hash_collisions(Fixture fixture) {
			_fixture = fixture;
		}

		[Fact]
		public async Task should_throw_wrong_expected_version() {
			const string stream1 = "account--696193173";
			const string stream2 = "LPN-FC002_LPK51001";

			var writeResult = await _fixture.Client.AppendToStreamAsync(
				stream1,
				AnyStreamRevision.NoStream,
				_fixture.CreateTestEvents());

			Assert.Equal(0, writeResult.NextExpectedVersion);

			//Write 100 events to stream 2 which will have the same hash as stream 1.
			for (var i = 0; i < 100; i++) {
				writeResult = await _fixture.Client.AppendToStreamAsync(
					stream2,
					AnyStreamRevision.Any,
					_fixture.CreateTestEvents());
				Assert.Equal(i, writeResult.NextExpectedVersion);
			}

			await _fixture.DisposeAsync();

			await using var fixture = new Fixture { DeleteDirectory = true };

			await fixture.InitializeAsync();

			await Assert.ThrowsAsync<WrongExpectedVersionException>(
				() => fixture.Client.AppendToStreamAsync(
					stream1,
					AnyStreamRevision.Any,
					_fixture.CreateTestEvents()));
		}

		public class Fixture : EventStoreGrpcFixture {
			private static readonly string PathName
				= $"{nameof(append_to_stream_with_hash_collisions)}-{Guid.NewGuid():n}";

			private readonly DirectoryInfo _directory;
			public bool DeleteDirectory { get; set; }

			public Fixture() : base(builder => builder
				.RunOnDisk(PathName)
				.WithHashCollisionReadLimitOf(1)
				.WithIndexBitnessVersion(Core.Index.PTableVersions.IndexV1)) {
				_directory = Directory.CreateDirectory(PathName);
			}

			protected override Task Given() => Task.CompletedTask;

			protected override Task When() => Task.CompletedTask;

			public override async Task DisposeAsync() {
				await base.DisposeAsync();

				if (DeleteDirectory) {
					_directory.Delete(true);
				}
			}
		}
	}
}
