using System.Collections.Generic;
using EventStore.Core.Index;
using NUnit.Framework;
using System;
using System.Linq;

namespace EventStore.Core.Tests.Index.IndexV1 {
	[TestFixture(PTableVersions.IndexV2, false)]
	[TestFixture(PTableVersions.IndexV2, true)]
	[TestFixture(PTableVersions.IndexV3, false)]
	[TestFixture(PTableVersions.IndexV3, true)]
	public class
		when_merging_ptables_with_entries_to_nonexisting_record_in_newer_index_versions :
			SpecificationWithDirectoryPerTestFixture {
		private readonly List<string> _files = new List<string>();
		private readonly List<PTable> _tables = new List<PTable>();
		protected PTable _newtable;
		protected byte _ptableVersion = PTableVersions.IndexV1;

		private bool _skipIndexVerify;

		public when_merging_ptables_with_entries_to_nonexisting_record_in_newer_index_versions(byte version,
			bool skipIndexVerify) {
			_ptableVersion = version;
			_skipIndexVerify = skipIndexVerify;
		}

		[OneTimeSetUp]
		public override void TestFixtureSetUp() {
			base.TestFixtureSetUp();
			for (int i = 0; i < 4; i++) {
				_files.Add(GetTempFilePath());

				var table = new HashListMemTable(_ptableVersion, maxSize: 30);
				for (int j = 0; j < 10; j++) {
					table.Add((ulong)(0x010100000000 << i), j, i * 10 + j);
				}

				_tables.Add(PTable.FromMemtable(table, _files[i], skipIndexVerify: _skipIndexVerify));
			}

			_files.Add(GetTempFilePath());
			_newtable = PTable.MergeTo(_tables, _files[4], (streamId, hash) => hash, x => x.Position % 2 == 0,
				x => new Tuple<string, bool>("", x.Position % 2 == 0), _ptableVersion,
				skipIndexVerify: _skipIndexVerify);
		}

		[OneTimeTearDown]
		public override void TestFixtureTearDown() {
			_newtable.Dispose();
			foreach (var ssTable in _tables) {
				ssTable.Dispose();
			}

			base.TestFixtureTearDown();
		}

		[Test]
		public void all_entries_are_left() {
			Assert.AreEqual(40, _newtable.Count);
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

		[Test]
		public void the_right_items_are_deleted() {
			for (int i = 0; i < 4; i++) {
				for (int j = 0; j < 10; j++) {
					long position;
					Assert.IsTrue(_newtable.TryGetOneValue((ulong)(0x010100000000 << i), j, out position));
					Assert.AreEqual(i * 10 + j, position);
				}
			}
		}
	}
}
