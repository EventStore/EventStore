using System;
using System.IO;
using EventStore.Core.Exceptions;
using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index
{
    [TestFixture]
    public class when_a_ptable_is_corrupt_on_disk: SpecificationWithDirectory
    {
        private string _filename;
        private PTable _table;
        private string _copiedfilename;

        [SetUp]
        public override void SetUp()
        {
            base.SetUp();

            _filename = GetTempFilePath();
            _copiedfilename = GetTempFilePath();
            var mtable = new HashListMemTable(maxSize: 10);
            mtable.Add(0x0101, 0x0001, 0x0001);
            mtable.Add(0x0105, 0x0001, 0x0002);
            _table = PTable.FromMemtable(mtable, _filename);
            _table.Dispose();
            File.Copy(_filename, _copiedfilename);
            using (var f = new FileStream(_copiedfilename, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
            {
                f.Seek(130, SeekOrigin.Begin);
                f.WriteByte(0x22);
            }
            _table = PTable.FromFile(_copiedfilename, 16);
        }

        [TearDown]
        public override void TearDown()
        {
            _table.MarkForDestruction();
            _table.WaitForDisposal(1000);

            base.TearDown();
        }

        [Test]
        public void the_hash_is_invalid()
        {
            var exc = Assert.Throws<CorruptIndexException>(() => _table.VerifyFileHash());
            Assert.IsInstanceOf<HashValidationException>(exc.InnerException);
        }
    }
}