using System;
using System.IO;
using NUnit.Framework;
using EventStore.Native;

namespace EventStore.Native.FileAccess.Tests
{
    //[TestFixture]
    // ReSharper disable once InconsistentNaming
    public class native_file_perf : IDisposable
    {
        private NativeFile _testFile;
        private const int PageSize = 4096;
        private const int BlockSize = PageSize * 16;
        private const int BufferSize = BlockSize * 2048;
        private NativeMethods.PageAlignedBuffer _blockBuffer;
        private FileStream _fileStream;
        private byte[] _arrayBuffer;

        //[OneTimeSetUp]
        public void CreateTestFile()
        {
            _testFile = NativeFile.OpenNewFile(
                Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()),
                BufferSize);
            _blockBuffer = TestUtil.GetRndBuffer(BufferSize);
            _fileStream = File.Create(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()));
            _fileStream.SetLength(BufferSize);
            _arrayBuffer = new byte[BufferSize];
            _blockBuffer.Buffer.CopyTo(_arrayBuffer, BufferSize);
        }

        //[Test]
        public void write_file_stream()
        {
            for (int i = 0; i < 10; i++)
            {
                _fileStream.Write(_arrayBuffer, 0, BufferSize);
            }
        }

        //[Test]
        public void write_block_multiple_unbuffered()
        {
            for (int i = 0; i < 10; i++)
            {
                _testFile.Write(0, _blockBuffer.Buffer, BufferSize);
            }
        }

        //[Test]
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
            var fName = _fileStream.Name;
            _fileStream?.Dispose();
            File.Delete(_testFile?.Info.FullName);
            File.Delete(fName);

        }
    }
}
