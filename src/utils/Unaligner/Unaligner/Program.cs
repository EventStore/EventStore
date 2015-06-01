using System;
using System.IO;
using EventStore.Core.TransactionLog.Chunks;

namespace Unaligner
{
    class Program
    {
        static void Main(string[] args)
        {

        }

        private void Unalignv2FileToOld(string filename)
        {
            //takes a v2 file and unaligns it so it can be used with previous versions
            try
            {
                SetAttributes(filename, false);
                using (var stream = new FileStream(filename, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
                {
                    Console.WriteLine("File size is {0}", stream.Length);
                    var header = ChunkHeader.FromStream(new MemoryStream(ReadHeader(stream)));
                    Console.WriteLine("Read header chunk {0} ({1}-{2} start: {3} end: {4}", 
                        header.ChunkId, 
                        header.ChunkStartNumber, 
                        header.ChunkEndNumber,
                        header.ChunkStartPosition,
                        header.ChunkEndPosition);
                    var footer = ChunkHeader.FromStream(new MemoryStream(ReadFooter(stream)));
                    Console.WriteLine("Read footer chunk {0} ({1}-{2} start: {3} end: {4} size: {5}",
                        footer.ChunkId,
                        footer.ChunkStartNumber,
                        footer.ChunkEndNumber,
                        footer.ChunkStartPosition,
                        footer.ChunkEndPosition,
                        footer.ChunkSize);
                }
            }
            finally
            {
                SetAttributes(filename, true);
            }
        }

        private byte[] ReadHeader(FileStream stream)
        {
            var header = new byte[ChunkHeader.Size];
            var footerStart = stream.Length - ChunkHeader.Size;
            stream.Seek(0, SeekOrigin.Begin);
            stream.Read(header, 0, ChunkHeader.Size);
            return header;            
        }

        private byte[] ReadFooter(FileStream stream)
        {
            var footer = new byte[ChunkFooter.Size];
            var footerStart = stream.Length - ChunkFooter.Size;
            stream.Seek(footerStart, SeekOrigin.Begin);
            stream.Read(footer, 0, ChunkFooter.Size);
            return footer;
        }

        private void SetAttributes(string filename, bool isReadOnly)
        {
            if (isReadOnly)
                File.SetAttributes(filename, FileAttributes.ReadOnly | FileAttributes.NotContentIndexed);
            else
                File.SetAttributes(filename, FileAttributes.NotContentIndexed);
        }
    }
}
