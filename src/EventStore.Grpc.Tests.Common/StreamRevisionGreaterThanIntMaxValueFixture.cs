using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Helpers;
using EventStore.Core.Messaging;
using EventStore.Core.Services;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.Chunks.TFChunk;
using EventStore.Core.TransactionLog.FileNamingStrategy;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Core.Util;
using EventStore.Grpc.Tests.Common;
using Xunit;

namespace EventStore.Grpc {
	public abstract class StreamRevisionGreaterThanIntMaxValueFixture : EventStoreGrpcFixture {
		protected abstract string StreamName { get; }

		protected override Task Given() => Client.SetStreamMetadataAsync(
			StreamName,
			AnyStreamRevision.NoStream,
			new StreamMetadata(truncateBefore: new StreamRevision(int.MaxValue + 1L)));

		protected StreamRevisionGreaterThanIntMaxValueFixture(Action<ICheckpoint, TFChunkWriter> runScenario) : base(
			builder => builder.RunOnDisk(CreateExistingDatabase(runScenario))) {
		}

		private static string CreateExistingDatabase(Action<ICheckpoint, TFChunkWriter> runScenario) {
			var databasePath = $"{nameof(StreamRevisionGreaterThanIntMaxValueFixture)}-{Guid.NewGuid():n}";

			var bus = new InMemoryBus("bus");
			var ioDispatcher = new IODispatcher(bus, new PublishEnvelope(bus));

			if (!Directory.Exists(databasePath))
				Directory.CreateDirectory(databasePath);

			var writerCheckFilename = Path.Combine(databasePath, Checkpoint.Writer + ".chk");
			var chaserCheckFilename = Path.Combine(databasePath, Checkpoint.Chaser + ".chk");
			var writerCheckpoint = Runtime.IsMono
				? (ICheckpoint)new FileCheckpoint(writerCheckFilename, Checkpoint.Writer, cached: true)
				: new MemoryMappedFileCheckpoint(writerCheckFilename, Checkpoint.Writer, cached: true);
			var chaserCheckpoint = Runtime.IsMono
				? (ICheckpoint)new FileCheckpoint(chaserCheckFilename, Checkpoint.Chaser, cached: true)
				: new MemoryMappedFileCheckpoint(chaserCheckFilename, Checkpoint.Chaser, cached: true);

			var db = new TFChunkDb(CreateDbConfig(databasePath, writerCheckpoint, chaserCheckpoint,
				TFConsts.ChunkSize));
			db.Open();

			// create DB
			var writer = new TFChunkWriter(db);
			writer.Open();
			runScenario(writerCheckpoint, writer);

			writer.Close();
			writerCheckpoint.Flush();
			chaserCheckpoint.Write(writerCheckpoint.Read());
			chaserCheckpoint.Flush();
			db.Close();

			return databasePath;
		}

		protected static void WriteSingleEvent(
			ICheckpoint writerCheckpoint,
			TFChunkWriter writer,
			string eventStreamId,
			long eventNumber,
			byte[] data,
			DateTime? timestamp = null,
			Guid eventId = default,
			string eventType = "some-type") {
			var prepare = LogRecord.SingleWrite(writerCheckpoint.ReadNonFlushed(),
				Guid.NewGuid(),
				eventId == default ? Guid.NewGuid() : eventId,
				eventStreamId,
				eventNumber - 1,
				eventType,
				data,
				null,
				timestamp);
			Assert.True(writer.Write(prepare, out _));
			var commit = LogRecord.Commit(
				writerCheckpoint.ReadNonFlushed(), prepare.CorrelationId, prepare.LogPosition, eventNumber);
			Assert.True(writer.Write(commit, out _));
		}


		private static TFChunkDbConfig CreateDbConfig(string pathName, long writerCheckpointPosition,
			long chaserCheckpointPosition = 0,
			long epochCheckpointPosition = -1, long truncateCheckpoint = -1, int chunkSize = 10000) {
			return new TFChunkDbConfig(pathName,
				new VersionedPatternFileNamingStrategy(pathName, "chunk-"),
				chunkSize,
				0,
				new InMemoryCheckpoint(writerCheckpointPosition),
				new InMemoryCheckpoint(chaserCheckpointPosition),
				new InMemoryCheckpoint(epochCheckpointPosition),
				new InMemoryCheckpoint(truncateCheckpoint),
				new InMemoryCheckpoint(-1),
				Constants.TFChunkInitialReaderCountDefault,
				Constants.TFChunkMaxReaderCountDefault
				);
		}

		private static TFChunkDbConfig CreateDbConfig(string pathName, ICheckpoint writerCheckpoint,
			ICheckpoint chaserCheckpoint, int chunkSize = 10000, ICheckpoint replicationCheckpoint = null) {
			if (replicationCheckpoint == null) replicationCheckpoint = new InMemoryCheckpoint(-1);
			return new TFChunkDbConfig(pathName,
				new VersionedPatternFileNamingStrategy(pathName, "chunk-"),
				chunkSize,
				0,
				writerCheckpoint,
				chaserCheckpoint,
				new InMemoryCheckpoint(-1),
				new InMemoryCheckpoint(-1),
				replicationCheckpoint,
				Constants.TFChunkInitialReaderCountDefault,
				Constants.TFChunkMaxReaderCountDefault
			);
		}

		private static TFChunk CreateNewChunk(string fileName, int chunkSize = 4096, bool isScavenged = false) {
			return TFChunk.CreateNew(fileName, chunkSize, 0, 0,
				isScavenged: isScavenged, inMem: false, unbuffered: false,
				writethrough: false, initialReaderCount: Constants.TFChunkInitialReaderCountDefault, Constants.TFChunkMaxReaderCountDefault, reduceFileCachePressure: false);
		}

		protected static IEnumerable<EventData> CreateTestEvents(int count)
			=> Enumerable.Range(0, count).Select(CreateTestEvent);

		protected static EventData CreateLinkToEvent(string originalStreamName, StreamRevision originalRevision)
			=> new EventData(Guid.NewGuid(), SystemEventTypes.LinkTo,
				Helper.UTF8NoBom.GetBytes($"{originalRevision.ToInt64()}@{originalStreamName}"));
	}
}
