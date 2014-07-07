using System;
using System.IO;
using System.Linq;
using EventStore.Core.Index;
using EventStore.Core.Tests.Fakes;
using EventStore.Core.TransactionLog;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index
{
    [TestFixture]
    public class adding_item_to_empty_index_map: SpecificationWithDirectoryPerTestFixture
    {
        private string _filename;
        private IndexMap _map;
        private string _tablename;
        private string _mergeFile;
        private MergeResult _result;

        [TestFixtureSetUp]
        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();

            _filename = GetTempFilePath();
            _tablename = GetTempFilePath();
            _mergeFile = GetFilePathFor("mergefile");

            _map = IndexMap.FromFile(_filename);
            var memtable = new HashListMemTable(maxSize: 10);
            memtable.Add(0, 1, 0);
            var table = PTable.FromMemtable(memtable, _tablename);
            _result = _map.AddPTable(table, 7, 11, _ => true, new FakeFilenameProvider(_mergeFile));
            table.MarkForDestruction();
        }

        [TestFixtureTearDown]
        public override void TestFixtureTearDown()
        {
            File.Delete(_filename);
            File.Delete(_mergeFile);
            File.Delete(_tablename);

            base.TestFixtureTearDown();
        }

        [Test]
        public void the_prepare_checkpoint_is_taken_from_the_latest_added_table()
        {
            Assert.AreEqual(7, _result.MergedMap.PrepareCheckpoint);
        }

        [Test]
        public void the_commit_checkpoint_is_taken_from_the_latest_added_table()
        {
            Assert.AreEqual(11, _result.MergedMap.CommitCheckpoint);
        }

        [Test]
        public void there_are_no_items_to_delete()
        {
            Assert.AreEqual(0, _result.ToDelete.Count);
        }

        [Test]
        public void the_merged_map_has_a_single_file()
        {
            Assert.AreEqual(1, _result.MergedMap.GetAllFilenames().Count());
            Assert.AreEqual(_tablename, _result.MergedMap.GetAllFilenames().ToList()[0]);
        }

        [Test]
        public void the_original_map_did_not_change()
        {
            Assert.AreEqual(0, _map.InOrder().Count());
            Assert.AreEqual(0, _map.GetAllFilenames().Count());
        }

        [Test]
        public void a_merged_file_was_not_created()
        {
            Assert.IsFalse(File.Exists(_mergeFile));
        }
    }
}