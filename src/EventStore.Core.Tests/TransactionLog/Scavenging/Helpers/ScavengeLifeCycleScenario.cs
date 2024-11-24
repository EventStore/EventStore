using System;
using System.Threading.Tasks;
using EventStore.Core.LogAbstraction;
using EventStore.Core.Tests.Services.Storage;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.Chunks;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Scavenging.Helpers {
	[TestFixture]
	abstract public class ScavengeLifeCycleScenario<TLogFormat, TStreamId> : SpecificationWithDirectoryPerTestFixture {
		protected TFChunkDb Db {
			get { return _dbResult.Db; }
		}

		private DbResult _dbResult;
		protected TFChunkScavenger<TStreamId> TfChunkScavenger;
		protected FakeTFScavengerLog Log;
		protected FakeTableIndex<TStreamId> FakeTableIndex;
		protected LogFormatAbstractor<TStreamId> _logFormat;

		public override async Task TestFixtureSetUp() {
			await base.TestFixtureSetUp();

			var indexDirectory = GetFilePathFor("index");
			_logFormat = LogFormatHelper<TLogFormat, TStreamId>.LogFormatFactory.Create(new() {
				IndexDirectory = indexDirectory,
			});

			var dbConfig = TFChunkHelper.CreateSizedDbConfig(PathName, 0, chunkSize: 1024 * 1024);
			var dbCreationHelper = new TFChunkDbCreationHelper<TLogFormat, TStreamId>(dbConfig, _logFormat);

			_dbResult = dbCreationHelper
				.Chunk().CompleteLastChunk()
				.Chunk().CompleteLastChunk()
				.Chunk()
				.CreateDb(commit: true);

			_dbResult.Db.Config.WriterCheckpoint.Flush();
			_dbResult.Db.Config.ChaserCheckpoint.Write(_dbResult.Db.Config.WriterCheckpoint.Read());
			_dbResult.Db.Config.ChaserCheckpoint.Flush();

			Log = new FakeTFScavengerLog();
			FakeTableIndex = new FakeTableIndex<TStreamId>();
			TfChunkScavenger = new TFChunkScavenger<TStreamId>(Serilog.Log.Logger, _dbResult.Db, Log, FakeTableIndex, new FakeReadIndex<TLogFormat, TStreamId>(_ => false, _logFormat.Metastreams),
				_logFormat.Metastreams, ITransactionFileTracker.NoOp);

			try {
				await When().WithTimeout(TimeSpan.FromMinutes(1));
			} catch (Exception ex) {
				throw new Exception("When Failed", ex);
			}
		}

		public override Task TestFixtureTearDown() {
			_logFormat?.Dispose();
			_dbResult.Db.Close();

			return base.TestFixtureTearDown();
		}

		protected abstract Task When();
	}
}
