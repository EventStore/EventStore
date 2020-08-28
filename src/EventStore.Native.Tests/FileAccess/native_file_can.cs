using System;
using System.IO;
using NUnit.Framework;
using EventStore.Native;

namespace EventStore.Native.FileAccess.Tests
{
    [TestFixture]
    // ReSharper disable once InconsistentNaming
    public class native_file_can : IDisposable
    {
        private NativeFile _testFile;
        private const int PageSize = 4096;
        private const int BlockSize = PageSize * 16;
        private NativeMethods.PageAlignedBuffer _singlePageBuffer;
        private NativeMethods.PageAlignedBuffer _twoPageBuffer;
        private NativeMethods.PageAlignedBuffer _singleBlockBuffer;
        private NativeMethods.PageAlignedBuffer _twoBlockBuffer;

        [OneTimeSetUp]
        public void CreateTestFile()
        {
            _testFile = NativeFile.OpenNewFile(
                Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()),
                BlockSize * 4);
            _singlePageBuffer = TestUtil.GetRndBuffer(PageSize);
            _twoPageBuffer = TestUtil.GetRndBuffer(PageSize * 2);
            _singleBlockBuffer = TestUtil.GetRndBuffer(BlockSize);
            _twoBlockBuffer = TestUtil.GetRndBuffer(BlockSize * 2);
        }
        //todo:add tests to confirm various writes do not alter other parts of file
        //todo: add test to confirm various ways write will become unbuffered
        [Test]
        public void write_page()
        {
            var readBuffer = TestUtil.GetFilledBuffer(PageSize, 0);
            _testFile.Write(0, _singlePageBuffer.Buffer, PageSize);
            _testFile.Read(0, readBuffer.Buffer, PageSize);
            Assert.IsTrue(TestUtil.BufferEqual(_singlePageBuffer.Buffer, readBuffer.Buffer, PageSize));
            _testFile.Write(0, readBuffer.Buffer, PageSize);
        }

        [Test]
        public void write_page_multiple()
        {
            var readBuffer = TestUtil.GetFilledBuffer(PageSize * 2, 0);
            _testFile.Write(0, _twoPageBuffer.Buffer, PageSize * 2);
            _testFile.Read(0, readBuffer.Buffer, PageSize * 2);
            Assert.IsTrue(TestUtil.BufferEqual(_twoPageBuffer.Buffer, readBuffer.Buffer, PageSize * 2));
            readBuffer.Buffer.ClearBuffer(PageSize * 2);
            _testFile.Write(0, readBuffer.Buffer, PageSize * 2);
        }

        [Test]
        public void write_block()
        {
            var readBuffer = TestUtil.GetFilledBuffer(BlockSize, 0);
            _testFile.Write(0, _singleBlockBuffer.Buffer, BlockSize);
            _testFile.Read(0, readBuffer.Buffer, BlockSize);
            Assert.IsTrue(TestUtil.BufferEqual(_singleBlockBuffer.Buffer, readBuffer.Buffer, BlockSize));
            readBuffer.Buffer.ClearBuffer(BlockSize);
            _testFile.Write(0, readBuffer.Buffer, BlockSize);
        }

        [Test]
        public void write_block_multiple()
        {
            var readBuffer = TestUtil.GetFilledBuffer(BlockSize * 2, 0);
            _testFile.Write(0, _twoBlockBuffer.Buffer, BlockSize * 2);
            _testFile.Read(0, readBuffer.Buffer, BlockSize * 2);
            Assert.IsTrue(TestUtil.BufferEqual(_twoBlockBuffer.Buffer, readBuffer.Buffer, BlockSize * 2));
            readBuffer.Buffer.ClearBuffer(BlockSize * 2);
            _testFile.Write(0, readBuffer.Buffer, BlockSize * 2);
        }
        [Test]
        public void write_block_plus()
        {
            var readBuffer = TestUtil.GetFilledBuffer(BlockSize * 2, 0);
            var length = BlockSize + PageSize;
            _testFile.Write(0, _twoBlockBuffer.Buffer, length);
            _testFile.Read(0, readBuffer.Buffer, length);
            Assert.IsTrue(TestUtil.BufferEqual(_twoBlockBuffer.Buffer, readBuffer.Buffer, length));
            readBuffer.Buffer.ClearBuffer(length);
            _testFile.Write(0, readBuffer.Buffer, length);
        }

        [Test]
        public void save_data()
        {
            long position = 0;
            _testFile.Write(position, _singlePageBuffer.Buffer, PageSize);

            var readBuffer = TestUtil.GetFilledBuffer(PageSize, 0);
            _testFile.Read(position, readBuffer.Buffer, PageSize);
            Assert.IsTrue(NativeMethods.Compare(_singlePageBuffer.Buffer, readBuffer.Buffer, PageSize) == 0);

        }
        [Test]
        public void save_data_at()
        {

            var tens = TestUtil.GetFilledBuffer(12, 10);
            var twenties = TestUtil.GetFilledBuffer(12, 20);
            var thirties = TestUtil.GetFilledBuffer(12, 30);
            var readBuffer = TestUtil.GetFilledBuffer(PageSize, 0);



            long position = 0;
            _testFile.Write(0, readBuffer.Buffer, PageSize);
            _testFile.Write(position, tens.Buffer, 12);
            _testFile.Write(position + 12, twenties.Buffer, 12);
            _testFile.Write(position + 12 + 12, thirties.Buffer, 12);

            _testFile.Read(position, readBuffer.Buffer, PageSize);
            var results = readBuffer;
            Assert.IsTrue(TestUtil.BufferEqual(results.Buffer, tens.Buffer, 12));
            Assert.IsTrue(TestUtil.BufferEqual(results.Buffer + 12, twenties.Buffer, 12));
            Assert.IsTrue(TestUtil.BufferEqual(results.Buffer + 24, thirties.Buffer, 12));
            Assert.IsTrue(TestUtil.BufferEqual(results.Buffer + 36, readBuffer.Buffer + 36, PageSize - 36));
        }

        [Test]
        public void virtual_alloc_is_page_aligned()
        {
            NativeMethods.PageAlignedBuffer buffer = new NativeMethods.PageAlignedBuffer();
            var pages = 1;
            try
            {
                buffer = NativeMethods.GetPageBuffer(pages);
                Assert.AreEqual(0, (long)buffer.Buffer % 4096);
            }
            finally
            {
                NativeMethods.FreeBuffer(ref buffer);
            }

        }

        public void Dispose()
        {
            _testFile?.Dispose();
            File.Delete(_testFile?.Info.FullName);
        }
    }
}
