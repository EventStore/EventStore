// Copyright (c) 2012, Event Store LLP
// All rights reserved.
// 
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
// 
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
// 
using System;
using System.IO;
using System.Linq;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Exceptions;

namespace EventStore.Core.TransactionLog.Chunks
{
    public class TFChunkDb: IDisposable
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<TFChunkDb>();

        public readonly TFChunkDbConfig Config;
        public readonly TFChunkManager Manager;

        public TFChunkDb(TFChunkDbConfig config)
        {
            Ensure.NotNull(config, "config");

            Config = config;
            Manager = new TFChunkManager(Config);
        }

        public void OpenVerifyAndClean(bool verifyHash = true)
        {
            var tempFiles = Config.FileNamingStrategy.GetAllTempFiles();
            for (int i = 0; i < tempFiles.Length; i++)
            {
                try
                {
                    File.Delete(tempFiles[i]);
                }
                catch(Exception exc)
                {
                    Log.ErrorException(exc, "Error while trying to delete remaining temp file: '{0}'.", tempFiles[i]);
                }
            }

            ValidateReaderChecksumsMustBeLess(Config);

            var checkpoint = Config.WriterCheckpoint.Read();

            var expectedFiles = (int)((checkpoint + Config.ChunkSize - 1) / Config.ChunkSize);
            var correctFileCount = expectedFiles;
            var onChunkBoundary = checkpoint > 0 && checkpoint % Config.ChunkSize == 0;
            if (checkpoint == 0 && Config.FileNamingStrategy.GetAllVersionsFor(0).Length == 0)
            {
                Manager.AddNewChunk();
                correctFileCount = expectedFiles = 1;
            }
            else
            {
                if (checkpoint == 0)
                    correctFileCount = expectedFiles = 1;

                for (int i=0; i<expectedFiles; ++i)
                {
                    var versions = Config.FileNamingStrategy.GetAllVersionsFor(i);
                    if (versions.Length == 0)
                    {
                        throw new CorruptDatabaseException(
                            new ChunkNotFoundException(Config.FileNamingStrategy.GetFilenameFor(i, 0)));
                    }

                    for (int j=1; j<versions.Length; ++j)
                    {
                        File.Delete(versions[j]);
                    }

                    var chunkFileName = versions[0];
                    if (i == expectedFiles - 1)
                    {
                        var chunk = LoadLastChunk(chunkFileName, verifyHash);
                        Manager.AddChunk(chunk);

                        if (onChunkBoundary)
                        {
                            if (!chunk.IsReadOnly)
                                chunk.Complete();

                            // there could be a new valid but empty chunk which could be corrupted
                            // so we can safely remove all its versions with no consequences
                            var files = Config.FileNamingStrategy.GetAllVersionsFor(expectedFiles);
                            for (int j = 0; j < files.Length; ++j)
                            {
                                File.Delete(files[j]);
                            }

                            Manager.AddNewChunk();
                            correctFileCount += 1;
                        }
                    }
                    else
                    {
                        var chunk = LoadChunk(chunkFileName, verifyHash);
                        Manager.AddChunk(chunk);
                    }
                }
            }

            EnsureNoOtherFiles(correctFileCount);

            Manager.EnableCaching();
        }

        public void OpenForRead(bool verifyHashes = true)
        {
            ValidateReaderChecksumsMustBeLess(Config);

            var checkpoint = Config.WriterCheckpoint.Read();

            if (checkpoint == 0 && Config.FileNamingStrategy.GetAllVersionsFor(0).Length == 0)
            {
                Manager.AddNewChunk();
            }
            else
            {
                var expectedFiles = (int)((checkpoint + Config.ChunkSize - 1) / Config.ChunkSize);
                if (checkpoint == 0)
                    expectedFiles = 1;
                for (int i = 0; i < expectedFiles; ++i)
                {
                    var versions = Config.FileNamingStrategy.GetAllVersionsFor(i);
                    if (versions.Length == 0)
                    {
                        throw new CorruptDatabaseException(
                            new ChunkNotFoundException(Config.FileNamingStrategy.GetFilenameFor(i, 0)));
                    }

                    var chunkFileName = versions[0];
                    if (i == expectedFiles - 1)
                    {
                        var chunk = LoadLastChunk(chunkFileName, verifyHashes);
                        Manager.AddChunk(chunk);
                    }
                    else
                    {
                        var chunk = LoadChunk(chunkFileName, verifyHash: verifyHashes);
                        Manager.AddChunk(chunk);
                    }
                }
            }

            Manager.EnableCaching();
        }

        private void ValidateReaderChecksumsMustBeLess(TFChunkDbConfig config)
        {
            var current = config.WriterCheckpoint.Read();
            foreach (var checkpoint in new[] { config.ChaserCheckpoint, config.EpochCheckpoint, config.TruncateCheckpoint})
            {
                if (checkpoint.Read() > current)
                    throw new CorruptDatabaseException(new ReaderCheckpointHigherThanWriterException(checkpoint.Name));
            }
        }

        private TFChunk.TFChunk LoadChunk(string chunkFileName, bool verifyHash)
        {
            var chunk = TFChunk.TFChunk.FromCompletedFile(chunkFileName, verifyHash);
            return chunk;
        }

        private TFChunk.TFChunk LoadLastChunk(string chunkFileName, bool verifyHash)
        {
            var pos = Config.WriterCheckpoint.Read();
            var writerPosition = (int)(pos % Config.ChunkSize);

            ChunkFooter chunkFooter;
            using (var fs = new FileStream(chunkFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (fs.Length < ChunkFooter.Size + ChunkHeader.Size)
                {
                    throw new CorruptDatabaseException(new BadChunkInDatabaseException(
                        string.Format("Chunk file '{0}' is bad. It even doesn't have enough size" 
                                      + " for header and footer, file size is {1} bytes.",
                                      chunkFileName,
                                      fs.Length)));
                }
                fs.Seek(-ChunkFooter.Size, SeekOrigin.End);
                chunkFooter = ChunkFooter.FromStream(fs);
            }
            
            if (chunkFooter.Completed && chunkFooter.MapSize > 0)
                return TFChunk.TFChunk.FromCompletedFile(chunkFileName, verifyHash);
            
            if (writerPosition == 0 && pos > 0)
                writerPosition = Config.ChunkSize;

            return TFChunk.TFChunk.FromOngoingFile(chunkFileName, writerPosition, checkSize: false);
        }

        private void EnsureNoOtherFiles(int expectedFiles)
        {
            var files = Config.FileNamingStrategy.GetAllPresentFiles();
            var actualFiles = files.Count();
            if (actualFiles != expectedFiles)
            {
                throw new CorruptDatabaseException(
                    new ExtraneousFileFoundException(string.Format("Expected file count: {0}, actual: {1}.",
                                                                   expectedFiles,
                                                                   actualFiles)));
            }
        }

        public void Dispose()
        {
            Close();
        }

        public void Close()
        {
            if (Manager != null)
                Manager.Dispose();
        }

    }
}
