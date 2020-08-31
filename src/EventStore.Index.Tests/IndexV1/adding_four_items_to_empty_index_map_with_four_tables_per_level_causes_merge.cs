using System.IO;
using System.Linq;
using System.Threading.Tasks;
using EventStore.Core.Index;
using EventStore.Core.TransactionLog.Tests.Helpers;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.IndexV1 {
	[TestFixture(PTableVersions.IndexV1, false)]
	[TestFixture(PTableVersions.IndexV1, true)]
	[TestFixture(PTableVersions.IndexV2, false)]
	[TestFixture(PTableVersions.IndexV2, true)]
	[TestFixture(PTableVersions.IndexV3, false)]
	[TestFixture(PTableVersions.IndexV3, true)]
	[TestFixture(PTableVersions.IndexV4, false)]
	[TestFixture(PTableVersions.IndexV4, true)]
	public class
		adding_four_items_to_empty_index_map_with_four_tables_per_level_causes_merge :
			SpecificationWithDirectoryPerTestFixture {
		private string _filename;
		private IndexMap _map;
		private string _mergeFile;
		private MergeResult _result;
		protected byte _ptableVersion = PTableVersions.IndexV1;
		private bool _skipIndexVerify;
		private int _maxAutoMergeIndexLevel = 4;

		public adding_four_items_to_empty_index_map_with_four_tables_per_level_causes_merge(byte version,
			bool skipIndexVerify) {
			_ptableVersion = version;
			_skipIndexVerify = skipIndexVerify;
		}

		[OneTimeSetUp]
		public override async Task TestFixtureSetUp() {
			await base.TestFixtureSetUp();

			_mergeFile = GetTempFilePath();
			_filename = GetTempFilePath();

			_map = IndexMapTestFactory.FromFile(_filename, maxTablesPerLevel: 4);
			var memtable = new HashListMemTable(_ptableVersion, maxSize: 10);
			memtable.Add(0, 1, 0);

			_result = _map.AddPTable(
				PTable.FromMemtable(memtable, GetTempFilePath(), Constants.PTableInitialReaderCount, Constants.PTableMaxReaderCountDefault, skipIndexVerify: _skipIndexVerify), 1, 2,
				(streamId, hash) => hash,
				_ => true,
				_ => new System.Tuple<string, bool>("", true),
				new GuidFilenameProvider(PathName),
				_ptableVersion,
				_maxAutoMergeIndexLevel,
				0,
				skipIndexVerify: _skipIndexVerify);
			_result.ToDelete.ForEach(x => x.MarkForDestruction());

			_result = _result.MergedMap.AddPTable(
				PTable.FromMemtable(memtable, GetTempFilePath(), Constants.PTableInitialReaderCount, Constants.PTableMaxReaderCountDefault, skipIndexVerify: _skipIndexVerify), 3, 4,
				(streamId, hash) => hash,
				_ => true, _ => new System.Tuple<string, bool>("", true), new GuidFilenameProvider(PathName),
				_ptableVersion, _maxAutoMergeIndexLevel, 0, skipIndexVerify: _skipIndexVerify);
			_result.ToDelete.ForEach(x => x.MarkForDestruction());

			_result = _result.MergedMap.AddPTable(
				PTable.FromMemtable(memtable, GetTempFilePath(), Constants.PTableInitialReaderCount, Constants.PTableMaxReaderCountDefault, skipIndexVerify: _skipIndexVerify), 4, 5,
				(streamId, hash) => hash,
				_ => true, _ => new System.Tuple<string, bool>("", true), new GuidFilenameProvider(PathName),
				_ptableVersion, _maxAutoMergeIndexLevel, 0, skipIndexVerify: _skipIndexVerify);
			_result.ToDelete.ForEach(x => x.MarkForDestruction());

			_result = _result.MergedMap.AddPTable(
				PTable.FromMemtable(memtable, GetTempFilePath(), Constants.PTableInitialReaderCount, Constants.PTableMaxReaderCountDefault, skipIndexVerify: _skipIndexVerify), 0, 1,
				(streamId, hash) => hash,
				_ => true, _ => new System.Tuple<string, bool>("", true), new FakeFilenameProvider(_mergeFile),
				_ptableVersion, _maxAutoMergeIndexLevel, 0, skipIndexVerify: _skipIndexVerify);
			_result.ToDelete.ForEach(x => x.MarkForDestruction());
		}

		[OneTimeTearDown]
		public override Task TestFixtureTearDown() {
			_result.MergedMap.InOrder().ToList().ForEach(x => x.MarkForDestruction());
			File.Delete(_filename);
			File.Delete(_mergeFile);

			return base.TestFixtureTearDown();
		}

		[Test]
		public void the_prepare_checkpoint_is_taken_from_the_latest_added_table() {
			Assert.AreEqual(0, _result.MergedMap.PrepareCheckpoint);
		}

		[Test]
		public void the_commit_checkpoint_is_taken_from_the_latest_added_table() {
			Assert.AreEqual(1, _result.MergedMap.CommitCheckpoint);
		}

		[Test]
		public void there_are_four_items_to_delete() {
			Assert.AreEqual(4, _result.ToDelete.Count);
		}

		[Test]
		public void the_merged_map_has_a_single_file() {
			Assert.AreEqual(1, _result.MergedMap.GetAllFilenames().Count());
			Assert.AreEqual(_mergeFile, _result.MergedMap.GetAllFilenames().ToList()[0]);
		}

		[Test]
		public void the_original_map_did_not_change() {
			Assert.AreEqual(0, _map.InOrder().Count());
			Assert.AreEqual(0, _map.GetAllFilenames().Count());
		}

		[Test]
		public void a_merged_file_was_created() {
			Assert.IsTrue(File.Exists(_mergeFile));
		}
	}
}
