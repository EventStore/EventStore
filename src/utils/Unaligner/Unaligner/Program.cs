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
            //takes a v2 file and aligns it so it can be used with unbuffered
            try
            {
                SetAttributes(filename, false);
                using (var stream = new FileStream(filename, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
                {
                    if (stream.Length % 4096 == 0) return;
                    var footerStart = stream.Length - ChunkFooter.Size;
                    var alignedSize = (stream.Length / 4096 + 1) * 4096;
                    var footer = new byte[ChunkFooter.Size];
                    stream.SetLength(alignedSize);
                    stream.Seek(footerStart, SeekOrigin.Begin);
                    stream.Read(footer, 0, ChunkFooter.Size);
                    stream.Seek(footerStart, SeekOrigin.Begin);
                    var bytes = new byte[alignedSize - footerStart - ChunkFooter.Size];
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Write(footer, 0, footer.Length);
                }
            }
            finally
            {
                SetAttributes(filename, true);
            }
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
