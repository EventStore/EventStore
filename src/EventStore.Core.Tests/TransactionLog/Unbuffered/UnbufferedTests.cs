using System;
using System.IO;
using EventStore.Common.Utils;
using EventStore.Core.TransactionLog.Unbuffered;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Unbuffered
{
    [TestFixture]
    public class UnbufferedTests : SpecificationWithDirectory
    {
        [Test]
        public void when_resizing_a_file()
        {
            var filename = GetFilePathFor(Guid.NewGuid().ToString());
            
            var stream = UnbufferedIOFileStream.Create(filename, FileMode.CreateNew, FileAccess.ReadWrite,
                FileShare.ReadWrite, false, 4096, false, 4096);
            stream.SetLength(4096 *1024);
            stream.Close();
            Assert.AreEqual(4096 * 1024, new FileInfo(filename).Length);
        }

        [Test]
        public void when_writing_less_than_buffer_and_closing()
        {
            var filename = GetFilePathFor(Guid.NewGuid().ToString());
            var bytes = GetBytes(255);
            var stream = UnbufferedIOFileStream.Create(filename, FileMode.CreateNew, FileAccess.ReadWrite,
                FileShare.ReadWrite, false, 4096, false, 4096);
            stream.Write(bytes, 0, bytes.Length);
            stream.Close();
            Assert.AreEqual(4096, new FileInfo(filename).Length);
            var read = File.ReadAllBytes(filename);
            for (var i = 0; i < 255; i++)
            {
                Assert.AreEqual(i % 255, read[i]);
            } 
        }

        [Test]
        public void when_writing_more_than_buffer_and_closing()
        {
            var filename = GetFilePathFor(Guid.NewGuid().ToString());
            var bytes = GetBytes(9000);
            var stream = UnbufferedIOFileStream.Create(filename, FileMode.CreateNew, FileAccess.ReadWrite,
                FileShare.ReadWrite, false, 4096, false, 4096);
            stream.Write(bytes, 0, bytes.Length);
            stream.Close();
            Assert.AreEqual(4096 * 3, new FileInfo(filename).Length);
            var read = File.ReadAllBytes(filename);
            for (var i = 0; i < 9000; i++)
            {
                Assert.AreEqual(i % 255, read[i]);
            }
        }

        private byte[] GetBytes(int size)
        {
            var ret = new byte[size];
            for (var i = 0; i < size; i++)
            {
                ret[i] = (byte) (i%255);
            }
            return ret;
        }
    }
}