using System;
using System.Threading.Tasks;
using EventStore.Core.Tests.Services.Storage;
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

		public override async Task TestFixtureSetUp() {
			await base.TestFixtureSetUp();

			var dbConfig = TFChunkHelper.CreateSizedDbConfig(PathName, 0, chunkSize: 1024 * 1024);
			var dbCreationHelper = new TFChunkDbCreationHelper<TLogFormat, TStreamId>(dbConfig);

			_dbResult = dbCreationHelper
				.Chunk().CompleteLastChunk()
				.Chunk().CompleteLastChunk()
				.Chunk()
				.CreateDb();

			_dbResult.Db.Config.WriterCheckpoint.Flush();
			_dbResult.Db.Config.ChaserCheckpoint.Write(_dbResult.Db.Config.WriterCheckpoint.Read());
			_dbResult.Db.Config.ChaserCheckpoint.Flush();

			Log = new FakeTFScavengerLog();
			FakeTableIndex = new FakeTableIndex<TStreamId>();
			TfChunkScavenger = new TFChunkScavenger<TStreamId>(_dbResult.Db, Log, FakeTableIndex, new FakeReadIndex<TLogFormat, TStreamId>(_ => false),
				LogFormatHelper<TLogFormat, TStreamId>.LogFormat.SystemStreams);

			try {
				await When().WithTimeout(TimeSpan.FromMinutes(1));
			} catch (Exception ex) {
				throw new Exception("When Failed", ex);
			}
		}

		public override Task TestFixtureTearDown() {
			_dbResult.Db.Close();

			return base.TestFixtureTearDown();
		}

		protected abstract Task When();
	}
}
