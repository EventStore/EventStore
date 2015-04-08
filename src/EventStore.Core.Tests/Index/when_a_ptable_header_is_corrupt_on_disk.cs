using System;
using System.IO;
using EventStore.Core.Exceptions;
using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index
{
    [TestFixture]
    public class when_a_ptable_header_is_corrupt_on_disk: SpecificationWithDirectory
    {
        private string _filename;
        private PTable _table;
        private string _copiedfilename;

        [SetUp]
        public void Setup()
        {
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
                f.Seek(22, SeekOrigin.Begin);
                f.WriteByte(0x22);
            }
            _table = PTable.FromFile(_copiedfilename, 16);
        }

        [Test]          
        public void the_hash_is_invalid()
        {
            var exc = Assert.Throws<CorruptIndexException>(() => _table.VerifyFileHash());
            Assert.IsInstanceOf<HashValidationException>(exc.InnerException);
        }

        [TearDown]
        public void Teardown()
        {
            _table.MarkForDestruction();
            _table.WaitForDisposal(1000);
            File.Delete(_filename);
            File.Delete(_copiedfilename);
        }
    }
}