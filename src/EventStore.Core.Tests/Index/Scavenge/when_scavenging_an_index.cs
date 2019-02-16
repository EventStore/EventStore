using System;
using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.Scavenge {
	[TestFixture(PTableVersions.IndexV2, false)]
	[TestFixture(PTableVersions.IndexV2, true)]
	[TestFixture(PTableVersions.IndexV3, false)]
	[TestFixture(PTableVersions.IndexV3, true)]
	[TestFixture(PTableVersions.IndexV4, false)]
	[TestFixture(PTableVersions.IndexV4, true)]
	public class when_scavenging_an_index : SpecificationWithDirectoryPerTestFixture {
		private PTable _newtable;
		private readonly byte _oldVersion;
		private bool _skipIndexVerify;
		private PTable _oldTable;

		public when_scavenging_an_index(byte oldVersion, bool skipIndexVerify) {
			_oldVersion = oldVersion;
			_skipIndexVerify = skipIndexVerify;
		}

		[OneTimeSetUp]
		public override void TestFixtureSetUp() {
			base.TestFixtureSetUp();

			var table = new HashListMemTable(_oldVersion, maxSize: 20);
			table.Add(0x010100000000, 0, 1);
			table.Add(0x010200000000, 0, 2);
			table.Add(0x010300000000, 0, 3);
			table.Add(0x010300000000, 1, 4);
			_oldTable = PTable.FromMemtable(table, GetTempFilePath());

			long spaceSaved;
			Func<IndexEntry, bool> existsAt = x => x.Position % 2 == 0;
			Func<IndexEntry, Tuple<string, bool>> readRecord = x => { throw new Exception("Should not be called"); };
			Func<string, ulong, ulong> upgradeHash = (streamId, hash) => {
				throw new Exception("Should not be called");
			};

			_newtable = PTable.Scavenged(_oldTable, GetTempFilePath(), upgradeHash, existsAt, readRecord,
				PTableVersions.IndexV4, out spaceSaved, skipIndexVerify: _skipIndexVerify);
		}

		[OneTimeTearDown]
		public override void TestFixtureTearDown() {
			_oldTable.Dispose();
			_newtable.Dispose();

			base.TestFixtureTearDown();
		}

		[Test]
		public void scavenged_ptable_is_newest_version() {
			Assert.AreEqual(PTableVersions.IndexV4, _newtable.Version);
		}

		[Test]
		public void there_are_2_records_in_the_merged_index() {
			Assert.AreEqual(2, _newtable.Count);
		}

		[Test]
		public void the_items_are_sorted() {
			var last = new IndexEntry(ulong.MaxValue, 0, long.MaxValue);
			foreach (var item in _newtable.IterateAllInOrder()) {
				Assert.IsTrue((last.Stream == item.Stream ? last.Version > item.Version : last.Stream > item.Stream) ||
				              ((last.Stream == item.Stream && last.Version == item.Version) &&
				               last.Position > item.Position));
				last = item;
			}
		}
	}
}
