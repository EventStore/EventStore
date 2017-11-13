using System.IO;
using System.Linq;
using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.IndexV1
{
    [TestFixture]
    public class merging_doesnt_occur_when_merging_is_disabled : SpecificationWithDirectoryPerTestFixture
    {
        private string _filename;
        private IndexMap _map;
        private MergeResult _result;
        protected byte _ptableVersion = PTableVersions.IndexV1;

        [OneTimeSetUp]
        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();

            _filename = GetTempFilePath();

            _map = IndexMap.FromFile(_filename, maxTablesPerLevel: 4);
            var memtable = new HashListMemTable(_ptableVersion, maxSize: 10);
            memtable.Add(0, 1, 0);

            _result = _map.AddPTable(PTable.FromMemtable(memtable, GetTempFilePath()), 1, 2,
                (streamId, hash) => hash,
                _ => true, _ => new System.Tuple<string, bool>("", true), new GuidFilenameProvider(PathName), _ptableVersion, mergeIfNecessary: false);
            _result.ToDelete.ForEach(x => x.MarkForDestruction());

            _result = _result.MergedMap.AddPTable(PTable.FromMemtable(memtable, GetTempFilePath()), 3, 4,
                (streamId, hash) => hash,
                _ => true, _ => new System.Tuple<string, bool>("", true), new GuidFilenameProvider(PathName), _ptableVersion, mergeIfNecessary: false);
            _result.ToDelete.ForEach(x => x.MarkForDestruction());

            _result = _result.MergedMap.AddPTable(PTable.FromMemtable(memtable, GetTempFilePath()), 4, 5,
                (streamId, hash) => hash,
                _ => true, _ => new System.Tuple<string, bool>("", true), new GuidFilenameProvider(PathName), _ptableVersion, mergeIfNecessary: false);
            _result.ToDelete.ForEach(x => x.MarkForDestruction());

            _result = _result.MergedMap.AddPTable(PTable.FromMemtable(memtable, GetTempFilePath()), 0, 1,
                (streamId, hash) => hash,
                _ => true, _ => new System.Tuple<string, bool>("", true), new GuidFilenameProvider(PathName), _ptableVersion, mergeIfNecessary: false);
            _result.ToDelete.ForEach(x => x.MarkForDestruction());
        }

        [OneTimeTearDown]
        public override void TestFixtureTearDown()
        {
            _result.MergedMap.InOrder().ToList().ForEach(x => x.MarkForDestruction());
            File.Delete(_filename);

            base.TestFixtureTearDown();
        }

        [Test]
        public void the_prepare_checkpoint_is_taken_from_the_latest_added_table()
        {
            Assert.AreEqual(0, _result.MergedMap.PrepareCheckpoint);
        }

        [Test]
        public void the_commit_checkpoint_is_taken_from_the_latest_added_table()
        {
            Assert.AreEqual(1, _result.MergedMap.CommitCheckpoint);
        }

        [Test]
        public void there_are_no_items_to_delete()
        {
            Assert.AreEqual(0, _result.ToDelete.Count);
        }

        [Test]
        public void the_map_has_four_files()
        {
            Assert.AreEqual(4, _result.MergedMap.GetAllFilenames().Count());
        }

        [Test]
        public void the_original_map_did_not_change()
        {
            Assert.AreEqual(0, _map.InOrder().Count());
            Assert.AreEqual(0, _map.GetAllFilenames().Count());
        }
    }
}