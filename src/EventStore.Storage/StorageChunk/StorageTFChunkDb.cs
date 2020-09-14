using System;
using System.IO;
using EventStore.Common.Utils;
using EventStore.Core.TransactionLogV2;
using EventStore.Core.TransactionLogV2.Checkpoint;
using EventStore.Core.TransactionLogV2.Chunks;
using EventStore.Core.TransactionLogV2.FileNamingStrategy;
using Serilog;

namespace EventStore.Core.Services.Storage.StorageChunk
{
	public class StorageTFChunkDb : TFChunkDb {
		public TFChunkDbConfig Config { get; }
		public TransactionLogV2.Chunks.TFChunkDb Db { get; }
		public TFChunkManager Manager { get; }
		public TFChunkDbTruncator Truncator { get; }

		public static StorageTFChunkDb Create(TFChunkDbConfig config) {
			return new StorageTFChunkDb(config);
		}

		public static StorageTFChunkDb Create (
			int chunkSize,
			int cachedChunks,
			string dbPath,
			long chunksCacheSize,
			bool inMemDb,
			int chunkInitialReaderCount,
			int chunkMaxReaderCount,
			bool optimizeReadSideCache,
			long maxTruncation,
			ILogger log) {
			ICheckpoint writerChk;
			ICheckpoint chaserChk;
			ICheckpoint epochChk;
			ICheckpoint truncateChk;
			//todo(clc) : promote these to file backed checkpoints re:project-io
			ICheckpoint replicationChk = new InMemoryCheckpoint(Checkpoint.Replication, initValue: -1);
			ICheckpoint indexChk = new InMemoryCheckpoint(Checkpoint.Replication, initValue: -1);
			if (inMemDb) {
				writerChk = new InMemoryCheckpoint(Checkpoint.Writer);
				chaserChk = new InMemoryCheckpoint(Checkpoint.Chaser);
				epochChk = new InMemoryCheckpoint(Checkpoint.Epoch, initValue: -1);
				truncateChk = new InMemoryCheckpoint(Checkpoint.Truncate, initValue: -1);
			} else {
				try {
					if (!Directory.Exists(dbPath)) // mono crashes without this check
						Directory.CreateDirectory(dbPath);
				} catch (UnauthorizedAccessException) {
					if (dbPath == Locations.DefaultDataDirectory) {
						log.Information(
							"Access to path {dbPath} denied. The Event Store database will be created in {fallbackDefaultDataDirectory}",
							dbPath, Locations.FallbackDefaultDataDirectory);
						dbPath = Locations.FallbackDefaultDataDirectory;
						log.Information("Defaulting DB Path to {dbPath}", dbPath);
			
						if (!Directory.Exists(dbPath)) // mono crashes without this check
							Directory.CreateDirectory(dbPath);
					} else {
						throw;
					}
				}

				var writerCheckFilename = Path.Combine(dbPath, Checkpoint.Writer + ".chk");
				var chaserCheckFilename = Path.Combine(dbPath, Checkpoint.Chaser + ".chk");
				var epochCheckFilename = Path.Combine(dbPath, Checkpoint.Epoch + ".chk");
				var truncateCheckFilename = Path.Combine(dbPath, Checkpoint.Truncate + ".chk");
				writerChk = new MemoryMappedFileCheckpoint(writerCheckFilename, Checkpoint.Writer, cached: true);
				chaserChk = new MemoryMappedFileCheckpoint(chaserCheckFilename, Checkpoint.Chaser, cached: true);
				epochChk = new MemoryMappedFileCheckpoint(epochCheckFilename, Checkpoint.Epoch, cached: true,
					initValue: -1);
				truncateChk = new MemoryMappedFileCheckpoint(truncateCheckFilename, Checkpoint.Truncate,
					cached: true, initValue: -1);
			}

			var cache = cachedChunks >= 0
				? cachedChunks * (long)(TFConsts.ChunkSize + ChunkHeader.Size + ChunkFooter.Size)
				: chunksCacheSize;

			var config = new TFChunkDbConfig(dbPath,
				new VersionedPatternFileNamingStrategy(dbPath, "chunk-"),
				chunkSize,
				cache,
				writerChk,
				chaserChk,
				epochChk,
				truncateChk,
				replicationChk,
				indexChk,
				chunkInitialReaderCount,
				chunkMaxReaderCount,
				inMemDb,
				optimizeReadSideCache,
				maxTruncation);
			return new StorageTFChunkDb(config);
		}

		private StorageTFChunkDb(TFChunkDbConfig config, bool isTransactionLogV3 = false) {
			if (isTransactionLogV3) {
				throw new NotImplementedException("Transaction Log v3 is not implemented yet");
			}
			
			Config = config;
			var v2Config = config.GetV2Config();
			Db = new TransactionLogV2.Chunks.TFChunkDb(v2Config);
			Manager = new TFChunkManager(v2Config);
			Truncator = new TFChunkDbTruncator(v2Config);
		}

		public void Open(bool verifyHash = true, bool readOnly = false, int threads = 1) {
			Db.Open(verifyHash, readOnly, threads);
		}

		public TFChunkReader GetReader() {
			return new TFChunkReader(Db, Config.WriterCheckpoint, optimizeReadSideCache: Db.Config.OptimizeReadSideCache);
		}

		public TFChunkWriter GetWriter() {
			return new TFChunkWriter(Db);
		}

		public ITransactionFileChaser GetChunkChaser() {
			return new TFChunkChaser(Db, Config.WriterCheckpoint, Config.ChaserCheckpoint,
				Config.OptimizeReadSideCache);
		}

		public void Close() {
			Db.Close();
		}

		public void Dispose() {
			Db.Dispose();
			Manager.Dispose();
		}
	}
}
