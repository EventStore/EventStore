using System;
using System.IO;
using System.Linq;
using EventStore.Core.Index;
using EventStore.Core.Util;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index
{
    [TestFixture]
    public class saving_index_with_single_item_to_a_file: SpecificationWithDirectoryPerTestFixture
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

            _filename = GetFilePathFor("indexfile");
            _tablename = GetTempFilePath();
            _mergeFile = GetFilePathFor("outputfile");

            _map = IndexMap.FromFile(_filename);
            var memtable = new HashListMemTable(maxSize: 10);
            memtable.Add(0, 2, 7);
            var table = PTable.FromMemtable(memtable, _tablename);
            _result = _map.AddPTable(table, 7, 11, _ => true, new FakeFilenameProvider(_mergeFile));
            _result.MergedMap.SaveToFile(_filename);
            _result.ToDelete.ForEach(x => x.Dispose());
            _result.MergedMap.InOrder().ToList().ForEach(x => x.Dispose());
            table.Dispose();
        }

        [TestFixtureTearDown]
        public override void TestFixtureTearDown()
        {
            _result.ToDelete.ForEach(x => x.MarkForDestruction());
            _result.MergedMap.InOrder().ToList().ForEach(x => x.MarkForDestruction());
            _result.MergedMap.InOrder().ToList().ForEach(x => x.WaitForDisposal(1000));
            base.TestFixtureTearDown();
        }

        [Test]
        public void the_file_exists()
        {
            Assert.IsTrue(File.Exists(_filename));
        }

        [Test]
        public void the_file_contains_correct_data()
        {
            using (var fs = File.OpenRead(_filename))
            using (var reader = new StreamReader(fs))
            {
                var text = reader.ReadToEnd();
                var lines = text.Replace("\r", "").Split('\n');

                fs.Position = 32;
                var md5 = MD5Hash.GetHashFor(fs);
                var md5String = BitConverter.ToString(md5).Replace("-", "");

                Assert.AreEqual(5, lines.Count());
                Assert.AreEqual(md5String, lines[0]);
                Assert.AreEqual(PTable.Version.ToString(), lines[1]);
                Assert.AreEqual("7/11", lines[2]);
                Assert.AreEqual("0,0," + Path.GetFileName(_tablename), lines[3]);
                Assert.AreEqual("", lines[4]);
            }
        }

        [Test]
        public void saved_file_could_be_read_correctly_and_without_errors()
        {
            var map = IndexMap.FromFile(_filename);
            map.InOrder().ToList().ForEach(x => x.Dispose());

            Assert.AreEqual(7, map.PrepareCheckpoint);
            Assert.AreEqual(11, map.CommitCheckpoint);
        }
    }
}