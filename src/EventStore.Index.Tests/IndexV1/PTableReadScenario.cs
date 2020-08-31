using EventStore.Core.Index;
using EventStore.Core.TransactionLog.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.IndexV1 {
	public abstract class PTableReadScenario : SpecificationWithFile {
		private readonly int _midpointCacheDepth;
		protected byte _ptableVersion = PTableVersions.IndexV1;

		protected PTable PTable;
		private bool _skipIndexVerify;

		protected PTableReadScenario(byte ptableVersion, bool skipIndexVerify, int midpointCacheDepth) {
			_ptableVersion = ptableVersion;
			_skipIndexVerify = skipIndexVerify;
			_midpointCacheDepth = midpointCacheDepth;
		}

		[SetUp]
		public override void SetUp() {
			base.SetUp();

			var table = new HashListMemTable(_ptableVersion, maxSize: 50);

			AddItemsForScenario(table);

			PTable = PTable.FromMemtable(table, Filename, Constants.PTableInitialReaderCount, Constants.PTableMaxReaderCountDefault, cacheDepth: _midpointCacheDepth,
				skipIndexVerify: _skipIndexVerify);
		}

		[TearDown]
		public override void TearDown() {
			PTable.Dispose();

			base.TearDown();
		}

		protected abstract void AddItemsForScenario(IMemTable memTable);
	}
}
