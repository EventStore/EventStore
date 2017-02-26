using System.IO;
using System.Linq;
using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.IndexV1
{
    [TestFixture]
    public class adding_sixteen_items_to_empty_index_map_with_four_tables_per_level_causes_double_merge: SpecificationWithDirectoryPerTestFixture
    {
        private string _filename;
        private IndexMap _map;
        private string _finalmergefile;
        private string _finalmergefile2;
        protected byte _ptableVersion = PTableVersions.IndexV1;

        private MergeResult _result;

        [TestFixtureSetUp]
        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();

            _filename = GetTempFilePath();
            _finalmergefile = GetTempFilePath();
            _finalmergefile2 = GetTempFilePath();

            _map = IndexMap.FromFile(_filename);
            var memtable = new HashListMemTable(_ptableVersion, maxSize: 10);
            memtable.Add(0, 1, 0);
            var guidFilename = new GuidFilenameProvider(PathName);
            _result = _map.AddPTable(PTable.FromMemtable(memtable, GetTempFilePath()), 0, 0, (streamId, hash) => hash, _ => true, _ => new System.Tuple<string, bool>("", true), guidFilename, _ptableVersion);
            _result.ToDelete.ForEach(x => x.MarkForDestruction());
            _result = _result.MergedMap.AddPTable(PTable.FromMemtable(memtable, GetTempFilePath()), 0, 0, (streamId, hash) => hash, _ => true, _ => new System.Tuple<string, bool>("", true), guidFilename, _ptableVersion);
            _result.ToDelete.ForEach(x => x.MarkForDestruction());
            _result = _result.MergedMap.AddPTable(PTable.FromMemtable(memtable, GetTempFilePath()), 0, 0, (streamId, hash) => hash, _ => true, _ => new System.Tuple<string, bool>("", true), guidFilename, _ptableVersion);
            _result.ToDelete.ForEach(x => x.MarkForDestruction());
            _result = _result.MergedMap.AddPTable(PTable.FromMemtable(memtable, GetTempFilePath()), 0, 0, (streamId, hash) => hash, _ => true, _ => new System.Tuple<string, bool>("", true), guidFilename, _ptableVersion);
            _result.ToDelete.ForEach(x => x.MarkForDestruction());
            _result = _result.MergedMap.AddPTable(PTable.FromMemtable(memtable, GetTempFilePath()), 0, 0, (streamId, hash) => hash, _ => true, _ => new System.Tuple<string, bool>("", true), guidFilename, _ptableVersion);
            _result.ToDelete.ForEach(x => x.MarkForDestruction());
            _result = _result.MergedMap.AddPTable(PTable.FromMemtable(memtable, GetTempFilePath()), 0, 0, (streamId, hash) => hash, _ => true, _ => new System.Tuple<string, bool>("", true), guidFilename, _ptableVersion);
            _result.ToDelete.ForEach(x => x.MarkForDestruction());
            _result = _result.MergedMap.AddPTable(PTable.FromMemtable(memtable, GetTempFilePath()), 0, 0, (streamId, hash) => hash, _ => true, _ => new System.Tuple<string, bool>("", true), guidFilename, _ptableVersion);
            _result.ToDelete.ForEach(x => x.MarkForDestruction());
            _result = _result.MergedMap.AddPTable(PTable.FromMemtable(memtable, GetTempFilePath()), 0, 0, (streamId, hash) => hash, _ => true, _ => new System.Tuple<string, bool>("", true), guidFilename, _ptableVersion);
            _result.ToDelete.ForEach(x => x.MarkForDestruction());
            _result = _result.MergedMap.AddPTable(PTable.FromMemtable(memtable, GetTempFilePath()), 0, 0, (streamId, hash) => hash, _ => true, _ => new System.Tuple<string, bool>("", true), guidFilename, _ptableVersion);
            _result.ToDelete.ForEach(x => x.MarkForDestruction());
            _result = _result.MergedMap.AddPTable(PTable.FromMemtable(memtable, GetTempFilePath()), 0, 0, (streamId, hash) => hash, _ => true, _ => new System.Tuple<string, bool>("", true), guidFilename, _ptableVersion);
            _result.ToDelete.ForEach(x => x.MarkForDestruction());
            _result = _result.MergedMap.AddPTable(PTable.FromMemtable(memtable, GetTempFilePath()), 0, 0, (streamId, hash) => hash, _ => true, _ => new System.Tuple<string, bool>("", true), guidFilename, _ptableVersion);
            _result.ToDelete.ForEach(x => x.MarkForDestruction());
            _result = _result.MergedMap.AddPTable(PTable.FromMemtable(memtable, GetTempFilePath()), 0, 0, (streamId, hash) => hash, _ => true, _ => new System.Tuple<string, bool>("", true), guidFilename, _ptableVersion);
            _result.ToDelete.ForEach(x => x.MarkForDestruction());
            _result = _result.MergedMap.AddPTable(PTable.FromMemtable(memtable, GetTempFilePath()), 0, 0, (streamId, hash) => hash, _ => true, _ => new System.Tuple<string, bool>("", true), guidFilename, _ptableVersion);
            _result.ToDelete.ForEach(x => x.MarkForDestruction());
            _result = _result.MergedMap.AddPTable(PTable.FromMemtable(memtable, GetTempFilePath()), 0, 0, (streamId, hash) => hash, _ => true, _ => new System.Tuple<string, bool>("", true), guidFilename, _ptableVersion);
            _result.ToDelete.ForEach(x => x.MarkForDestruction());
            _result = _result.MergedMap.AddPTable(PTable.FromMemtable(memtable, GetTempFilePath()), 0, 0, (streamId, hash) => hash, _ => true, _ => new System.Tuple<string, bool>("", true), guidFilename, _ptableVersion);
            _result.ToDelete.ForEach(x => x.MarkForDestruction());
            _result = _result.MergedMap.AddPTable(PTable.FromMemtable(memtable, GetTempFilePath()), 1, 2, (streamId, hash) => hash, _ => true, _ => new System.Tuple<string, bool>("", true), new FakeFilenameProvider(_finalmergefile, _finalmergefile2), _ptableVersion);
            _result.ToDelete.ForEach(x => x.MarkForDestruction());
        }

        [TestFixtureTearDown]
        public override void TestFixtureTearDown()
        {
            _result.MergedMap.InOrder().ToList().ForEach(x => x.MarkForDestruction());
            File.Delete(_filename);

            base.TestFixtureTearDown();
        }

        [Test]
        public void the_prepare_checkpoint_is_taken_from_the_latest_added_table()
        {
            Assert.AreEqual(1, _result.MergedMap.PrepareCheckpoint);
        }

        [Test]
        public void the_commit_checkpoint_is_taken_from_the_latest_added_table()
        {
            Assert.AreEqual(2, _result.MergedMap.CommitCheckpoint);
        }

        [Test]
        public void there_are_eight_items_to_delete()
        {
            Assert.AreEqual(8, _result.ToDelete.Count);
        }

        [Test]
        public void the_merged_map_has_a_single_file()
        {
            Assert.AreEqual(1, _result.MergedMap.GetAllFilenames().Count());
            Assert.AreEqual(_finalmergefile2, _result.MergedMap.GetAllFilenames().ToList()[0]);
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
            Assert.IsTrue(File.Exists(_finalmergefile2));
        }
    }
}