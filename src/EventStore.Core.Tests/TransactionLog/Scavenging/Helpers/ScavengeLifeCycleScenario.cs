using EventStore.Core.Tests.Services.Storage;
using EventStore.Core.TransactionLog.Chunks;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Scavenging.Helpers {
	[TestFixture]
	abstract class ScavengeLifeCycleScenario : SpecificationWithDirectoryPerTestFixture {
		protected TFChunkDb Db {
			get { return _dbResult.Db; }
		}

		private DbResult _dbResult;
		protected TFChunkScavenger TfChunkScavenger;
		protected FakeTFScavengerLog Log;
		protected FakeTableIndex FakeTableIndex;

		public override void TestFixtureSetUp() {
			base.TestFixtureSetUp();

			var dbConfig = TFChunkHelper.CreateDbConfig(PathName, 0, chunkSize: 1024 * 1024);
			var dbCreationHelper = new TFChunkDbCreationHelper(dbConfig);

			_dbResult = dbCreationHelper
				.Chunk().CompleteLastChunk()
				.Chunk().CompleteLastChunk()
				.Chunk()
				.CreateDb();

			_dbResult.Db.Config.WriterCheckpoint.Flush();
			_dbResult.Db.Config.ChaserCheckpoint.Write(_dbResult.Db.Config.WriterCheckpoint.Read());
			_dbResult.Db.Config.ChaserCheckpoint.Flush();

			Log = new FakeTFScavengerLog();
			FakeTableIndex = new FakeTableIndex();
			TfChunkScavenger = new TFChunkScavenger(_dbResult.Db, Log, FakeTableIndex, new FakeReadIndex(_ => false));

			When();
		}

		public override void TestFixtureTearDown() {
			_dbResult.Db.Close();

			base.TestFixtureTearDown();
		}

		protected abstract void When();
	}
}
