using System;
using System.IO;
using EventStore.Core.TransactionLog.Checkpoint;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.FileNamingStrategy;

namespace Unaligner
{
    class Program
    {
        static void Main(string[] args)
        {
            if (args.Length != 1)
            {
                PrintUsage();
                Die("");
            }
            var path = args[0];
            var writerfile = Path.Combine(path,"writer.chk");
            if (!File.Exists(writerfile))
            {
                Die("unable to find writer checkpoint " + writerfile);
            }
            var checkpoint = new FileCheckpoint("writer.chk");
            var writtenTo = checkpoint.Read();
            Console.WriteLine("Writer checkpoint is {0}", writtenTo);
            var namesStrategy = new VersionedPatternFileNamingStrategy(path, "chunk-");
            foreach (var item in namesStrategy.GetAllPresentFiles())
            {
                Unalignv2FileToOld(item);  
            }
        }

        private static void PrintUsage()
        {
            Console.WriteLine("Usage: unalign pathtodb");
            Console.WriteLine(new string('*', 80));
            Console.WriteLine("WARNING THIS PROGRAM ALTERS CHUNK FILES USE ONLY IN OFFLINE MODE AND BACK UP FIRST");
            Console.WriteLine(new string('*', 80));
        }

        private static void Die(string message)
        {
            Console.WriteLine(message);
            Environment.Exit(1);
        }

        static void Unalignv2FileToOld(string filename)
        {
            //takes a v2 file and unaligns it so it can be used with previous versions
            try
            {
                SetAttributes(filename, false);
                using (var stream = new FileStream(filename, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
                {
                    Console.WriteLine("\n\nFile name '{0}' size is {1}", filename, stream.Length);
                    var header = ChunkHeader.FromStream(new MemoryStream(ReadHeader(stream)));
                    Console.WriteLine("Read header chunk {0} ({1}-{2} start: {3} end: {4} size : {5}", 
                        header.ChunkId, 
                        header.ChunkStartNumber, 
                        header.ChunkEndNumber,
                        header.ChunkStartPosition,
                        header.ChunkEndPosition,
                        header.ChunkSize);
                    var footer = ChunkFooter.FromStream(new MemoryStream(ReadFooter(stream)));
                    Console.WriteLine("Read footer chunk completed: {0} log size: {1} phys size: {2} map size {3} is12? {4} hash {5}",
                        footer.IsCompleted,
                        footer.LogicalDataSize,
                        footer.PhysicalDataSize,
                        footer.MapSize,
                        footer.IsMap12Bytes,
                        footer.MD5Hash);
                    if (!footer.IsCompleted)
                    {
                        Console.WriteLine("Not truncating chunk as its not completed.");
                        return;
                    }
                    var length = ChunkHeader.Size + footer.PhysicalDataSize + footer.MapSize + ChunkFooter.Size;
                    Console.WriteLine("setting length to {0}", length);
                    //do truncate
                    stream.SetLength(length);
                    stream.Seek(length - ChunkFooter.Size, SeekOrigin.Begin);
                    var footbytes = footer.AsByteArray();
                    stream.Write(footbytes,0, footbytes.Length);
                }
            }
            finally
            {
                SetAttributes(filename, true);
            }
        }

        static byte[] ReadHeader(FileStream stream)
        {
            var header = new byte[ChunkHeader.Size];
            var footerStart = stream.Length - ChunkHeader.Size;
            stream.Seek(0, SeekOrigin.Begin);
            stream.Read(header, 0, ChunkHeader.Size);
            return header;            
        }

        static byte[] ReadFooter(FileStream stream)
        {
            var footer = new byte[ChunkFooter.Size];
            var footerStart = stream.Length - ChunkFooter.Size;
            stream.Seek(footerStart, SeekOrigin.Begin);
            stream.Read(footer, 0, ChunkFooter.Size);
            return footer;
        }

        static void SetAttributes(string filename, bool isReadOnly)
        {
            if (isReadOnly)
                File.SetAttributes(filename, FileAttributes.ReadOnly | FileAttributes.NotContentIndexed);
            else
                File.SetAttributes(filename, FileAttributes.NotContentIndexed);
        }
    }
}
