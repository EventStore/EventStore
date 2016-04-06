using System;
using System.Linq;
using System.IO;
using NUnit.Framework;
using EventStore.Core.Index;

namespace EventStore.Core.Tests.Index
{
    [TestFixture, Explicit]
    public class opening_a_ptable_with_more_than_32bits_of_records: SpecificationWithFilePerTestFixture
    {
        private PTable _ptable;

        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();
            var header = new PTableHeader(1);
            using(var f = File.Open(Filename, FileMode.OpenOrCreate)) {
                f.Seek(0, SeekOrigin.Begin);
                var bytes = header.AsByteArray();
                f.Write(bytes, 0, bytes.Length);
                var size = (long) (uint.MaxValue + 10000000L) * (long) PTable.IndexEntrySize + PTableHeader.Size + PTable.MD5Size;
                Console.WriteLine("allocating file " + Filename + " size is " + size);
                f.SetLength(size);
                Console.WriteLine("file allocated");
            }
            _ptable = PTable.FromFile(Filename, 22);
        }

        public override void TestFixtureTearDown()
        {
            _ptable.Dispose();
            base.TestFixtureTearDown();
        }

        [Test, Explicit]
        public void count_should_be_right()
        {
            Assert.AreEqual((long) uint.MaxValue + 10000000L, _ptable.Count);
        }

        [Test, Explicit]
        public void filename_is_correct()
        {
            Assert.AreEqual(Filename, _ptable.Filename);
        }
    }
}