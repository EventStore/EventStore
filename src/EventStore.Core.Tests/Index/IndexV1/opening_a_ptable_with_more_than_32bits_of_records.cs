using System;
using System.Security.Cryptography;
using System.Diagnostics;
using System.IO;
using NUnit.Framework;
using EventStore.Core.Index;
using EventStore.Common.Utils;

namespace EventStore.Core.Tests.Index.IndexV1
{
    [TestFixture, Explicit]
    public class opening_a_ptable_with_more_than_32bits_of_records : SpecificationWithFilePerTestFixture
    {
        public const int MD5Size = 16;
        public const byte Version = 1;
        public const int DefaultSequentialBufferSize = 65536;

        private PTable _ptable;
        private long _size;
        private long _ptableCount;

        protected int indexEntrySize = PTable.IndexEntryV1Size;

        public override void TestFixtureSetUp()
        {
            base.TestFixtureSetUp();
            _ptableCount = (long)(uint.MaxValue + 10000000L);
            _size = _ptableCount * (long)indexEntrySize + PTableHeader.Size + PTable.MD5Size;
            Console.WriteLine("Creating PTable at {0}. Size of PTable: {1}", Filename, _size);
            CreatePTableFile(Filename, _size, indexEntrySize);
            _ptable = PTable.FromFile(Filename, 22);
        }

        public static void CreatePTableFile(string filename, long ptableSize, int indexEntrySize, int cacheDepth = 16)
        {
            Ensure.NotNullOrEmpty(filename, "filename");
            Ensure.Nonnegative(cacheDepth, "cacheDepth");

            var sw = Stopwatch.StartNew();
            var tableId = Guid.NewGuid();
            using (var fs = new FileStream(filename, FileMode.Create, FileAccess.ReadWrite, FileShare.None,
                                           DefaultSequentialBufferSize, FileOptions.SequentialScan))
            {
                fs.SetLength((long)ptableSize);
                fs.Seek(0, SeekOrigin.Begin);

                var recordCount = (long)((ptableSize - PTableHeader.Size - PTable.MD5Size) / (long)indexEntrySize);
                using (var md5 = MD5.Create())
                using (var cs = new CryptoStream(fs, md5, CryptoStreamMode.Write))
                using (var bs = new BufferedStream(cs, DefaultSequentialBufferSize))
                {
                    // WRITE HEADER
                    var headerBytes = new PTableHeader(Version).AsByteArray();
                    cs.Write(headerBytes, 0, headerBytes.Length);

                    // WRITE INDEX ENTRIES
                    var buffer = new byte[indexEntrySize];
                    for (long i = 0; i < recordCount; i++)
                    {
                        bs.Write(buffer, 0, indexEntrySize);
                    }
                    bs.Flush();
                    cs.FlushFinalBlock();

                    // WRITE MD5
                    var hash = md5.Hash;
                    fs.Write(hash, 0, hash.Length);
                }
            }
            Console.WriteLine("Created PTable File[{0}, size of {1}] in {2}.", tableId, ptableSize, sw.Elapsed);
        }

        public override void TestFixtureTearDown()
        {
            _ptable.Dispose();
            base.TestFixtureTearDown();
        }

        [Test, Explicit]
        public void count_should_be_right()
        {
            Assert.AreEqual(_ptableCount, _ptable.Count);
        }

        [Test, Explicit]
        public void filename_is_correct()
        {
            Assert.AreEqual(Filename, _ptable.Filename);
        }
    }
}