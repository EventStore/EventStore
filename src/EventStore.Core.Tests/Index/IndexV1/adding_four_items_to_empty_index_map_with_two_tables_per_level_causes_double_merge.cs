using System.IO;
using System.Linq;
using EventStore.Core.Index;
using NUnit.Framework;
using System;

namespace EventStore.Core.Tests.Index.IndexV1
{
    [TestFixture]
    public class adding_four_items_to_empty_index_map_with_two_tables_per_level_causes_double_merge: SpecificationWithDirectoryPerTestFixture
    {
        private string _filename;
        private IndexMap _map;
        private string _mergeFile;
        private MergeResult _result;
        protected byte _ptableVersion = PTableVersions.IndexV1;

        [TestFixtureSetUp]
        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();

            _mergeFile = GetTempFilePath();
            _filename = GetTempFilePath();

            _map = IndexMap.FromFile(_filename, maxTablesPerLevel: 2);
            var memtable = new HashListMemTable(_ptableVersion, maxSize: 10);
            memtable.Add(0, 1, 0);

            _result = _map.AddPTable(PTable.FromMemtable(memtable, GetTempFilePath()),
                                     10, 20, (streamId, hash) => hash, _ => true, _ => new Tuple<string, bool>("", true), new GuidFilenameProvider(PathName), _ptableVersion);
            _result.ToDelete.ForEach(x => x.MarkForDestruction());
            _result = _result.MergedMap.AddPTable(PTable.FromMemtable(memtable, GetTempFilePath()),
                                                  20, 30, (streamId, hash) => hash, _ => true, _ => new Tuple<string, bool>("", true), new GuidFilenameProvider(PathName), _ptableVersion);
            _result.ToDelete.ForEach(x => x.MarkForDestruction());
            _result = _result.MergedMap.AddPTable(PTable.FromMemtable(memtable, GetTempFilePath()),
                                                  30, 40, (streamId, hash) => hash, _ => true, _ => new Tuple<string, bool>("", true), new GuidFilenameProvider(PathName), _ptableVersion);
            _result.ToDelete.ForEach(x => x.MarkForDestruction());
            _result = _result.MergedMap.AddPTable(PTable.FromMemtable(memtable, GetTempFilePath()),
                                                  50, 60, (streamId, hash) => hash, _ => true, _ => new Tuple<string, bool>("", true), new FakeFilenameProvider(_mergeFile + ".firstmerge", _mergeFile), _ptableVersion);
            _result.ToDelete.ForEach(x => x.MarkForDestruction());
        }

        [TestFixtureTearDown]
        public override void TestFixtureTearDown()
        {
            _result.MergedMap.InOrder().ToList().ForEach(x => x.MarkForDestruction());
            File.Delete(_filename);
            File.Delete(_mergeFile);

            base.TestFixtureTearDown();
        }

        [Test]
        public void the_prepare_checkpoint_is_taken_from_the_latest_added_table()
        {
            Assert.AreEqual(50, _result.MergedMap.PrepareCheckpoint);
        }

        [Test]
        public void the_commit_checkpoint_is_taken_from_the_latest_added_table()
        {
            Assert.AreEqual(60, _result.MergedMap.CommitCheckpoint);
        }

        [Test]
        public void there_are_four_items_to_delete()
        {
            Assert.AreEqual(4, _result.ToDelete.Count);
        }

        [Test]
        public void the_merged_map_has_a_single_file()
        {
            Assert.AreEqual(1, _result.MergedMap.GetAllFilenames().Count());
            Assert.AreEqual(_mergeFile, _result.MergedMap.GetAllFilenames().ToList()[0]);
        }

        [Test]
        public void the_original_map_did_not_change()
        {
            Assert.AreEqual(0, _map.InOrder().Count());
            Assert.AreEqual(0, _map.GetAllFilenames().Count());
        }

        [Test]
        public void a_merged_file_was_created()
        {
            Assert.IsTrue(File.Exists(_mergeFile));
        }
    }
}