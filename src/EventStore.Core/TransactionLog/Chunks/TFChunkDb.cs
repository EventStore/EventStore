﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
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

        public void Open(bool verifyHash = true, bool readOnly = false)
        {
            ValidateReaderChecksumsMustBeLess(Config);
            var checkpoint = Config.WriterCheckpoint.Read();

            if (Config.InMemDb)
            {
                Manager.AddNewChunk();
                return;
            }

            var lastChunkNum = (int) (checkpoint/Config.ChunkSize);
            var lastChunkVersions = Config.FileNamingStrategy.GetAllVersionsFor(lastChunkNum);

            for (int chunkNum = 0; chunkNum < lastChunkNum; )
            {
                var versions = Config.FileNamingStrategy.GetAllVersionsFor(chunkNum);
                if (versions.Length == 0)
                    throw new CorruptDatabaseException(new ChunkNotFoundException(Config.FileNamingStrategy.GetFilenameFor(chunkNum, 0)));

                TFChunk.TFChunk chunk;
                if (lastChunkVersions.Length == 0 && (chunkNum + 1) * (long)Config.ChunkSize == checkpoint)
                {
                    // The situation where the logical data size is exactly divisible by ChunkSize,
                    // so it might happen that we have checkpoint indicating one more chunk should exist,
                    // but the actual last chunk is (lastChunkNum-1) one and it could be not completed yet -- perfectly valid situation.
                    var footer = ReadChunkFooter(versions[0]);
                    if (footer.IsCompleted)
                        chunk = TFChunk.TFChunk.FromCompletedFile(versions[0], verifyHash: false, unbufferedRead:Config.Unbuffered, initialReaderCount:Config.InitialReaderCount);
                    else
                    {
                        chunk = TFChunk.TFChunk.FromOngoingFile(versions[0], Config.ChunkSize, checkSize: false, unbuffered:Config.Unbuffered, writethrough:Config.WriteThrough, initialReaderCount:Config.InitialReaderCount);
                        // chunk is full with data, we should complete it right here
                        if (!readOnly)
                            chunk.Complete();
                    }
                }
                else
                {
                    chunk = TFChunk.TFChunk.FromCompletedFile(versions[0], verifyHash: false, unbufferedRead:Config.Unbuffered, initialReaderCount:Config.InitialReaderCount);
                }
                Manager.AddChunk(chunk);
                chunkNum = chunk.ChunkHeader.ChunkEndNumber + 1;
            }

            if (lastChunkVersions.Length == 0)
            {
                var onBoundary = checkpoint == (Config.ChunkSize * (long)lastChunkNum);
                if (!onBoundary)
                    throw new CorruptDatabaseException(new ChunkNotFoundException(Config.FileNamingStrategy.GetFilenameFor(lastChunkNum, 0)));
                if (!readOnly)
                    Manager.AddNewChunk();
            }
            else
            {
                var chunkFileName = lastChunkVersions[0];
                var chunkHeader = ReadChunkHeader(chunkFileName);
                var chunkLocalPos = chunkHeader.GetLocalLogPosition(checkpoint);
                if (chunkHeader.IsScavenged)
                {
                    var lastChunk = TFChunk.TFChunk.FromCompletedFile(chunkFileName, verifyHash: false, unbufferedRead:Config.Unbuffered, initialReaderCount:Config.InitialReaderCount);
                    if (lastChunk.ChunkFooter.LogicalDataSize != chunkLocalPos)
                    {
                        lastChunk.Dispose();
                        throw new CorruptDatabaseException(new BadChunkInDatabaseException(
                            string.Format("Chunk {0} is corrupted. Expected local chunk position: {1}, "
                                          + "but Chunk.LogicalDataSize is {2} (Chunk.PhysicalDataSize is {3}). Writer checkpoint: {4}.",
                                          chunkFileName, chunkLocalPos, lastChunk.LogicalDataSize, lastChunk.PhysicalDataSize, checkpoint)));
                    }
                    Manager.AddChunk(lastChunk);
                    if (!readOnly)
                    {
                        Log.Info("Moving WriterCheckpoint from {0} to {1}, as it points to the scavenged chunk. "
                                 + "If that was not caused by replication of scavenged chunks, that could be a bug.",
                                 checkpoint, lastChunk.ChunkHeader.ChunkEndPosition);
                        Config.WriterCheckpoint.Write(lastChunk.ChunkHeader.ChunkEndPosition);
                        Config.WriterCheckpoint.Flush();
                        Manager.AddNewChunk();
                    }
                }
                else
                {
                    var lastChunk = TFChunk.TFChunk.FromOngoingFile(chunkFileName, (int)chunkLocalPos, checkSize: false, unbuffered:Config.Unbuffered, writethrough:Config.WriteThrough, initialReaderCount:Config.InitialReaderCount);
                    Manager.AddChunk(lastChunk);
                }
            }

            EnsureNoExcessiveChunks(lastChunkNum);

            if (!readOnly)
            {
                RemoveOldChunksVersions(lastChunkNum);
                CleanUpTempFiles();
            }

            if (verifyHash && lastChunkNum > 0)
            {
                var preLastChunk = Manager.GetChunk(lastChunkNum - 1);
                var lastBgChunkNum = preLastChunk.ChunkHeader.ChunkStartNumber - 1;
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    for (int chunkNum = lastBgChunkNum; chunkNum >= 0; )
                    {
                        var chunk = Manager.GetChunk(chunkNum);
                        try
                        {
                            chunk.VerifyFileHash();
                        }
                        catch (FileBeingDeletedException exc)
                        {
                            Log.Trace("{0} exception was thrown while doing background validation of chunk {1}.",
                                      exc.GetType().Name, chunk);
                            Log.Trace("That's probably OK, especially if truncation was request at the same time: {0}.",
                                      exc.Message);
                        }
                        catch (Exception exc)
                        {
                            var msg = string.Format("Verification of chunk {0} failed, terminating server...", chunk);
                            Log.FatalException(exc, msg);
                            Application.Exit(ExitCode.Error, msg);
                            return;
                        }
                        chunkNum = chunk.ChunkHeader.ChunkStartNumber - 1;
                    }
                });
            }

            Manager.EnableCaching();
        }

        private void ValidateReaderChecksumsMustBeLess(TFChunkDbConfig config)
        {
            var current = config.WriterCheckpoint.Read();
            foreach (var checkpoint in new[] { config.ChaserCheckpoint, config.EpochCheckpoint })
            {
                if (checkpoint.Read() > current)
                    throw new CorruptDatabaseException(new ReaderCheckpointHigherThanWriterException(checkpoint.Name));
            }
        }

        private static ChunkHeader ReadChunkHeader(string chunkFileName)
        {
            ChunkHeader chunkHeader;
            using (var fs = new FileStream(chunkFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (fs.Length < ChunkFooter.Size + ChunkHeader.Size)
                {
                    throw new CorruptDatabaseException(new BadChunkInDatabaseException(
                        string.Format("Chunk file '{0}' is bad. It does not have enough size for header and footer. File size is {1} bytes.",
                                      chunkFileName, fs.Length)));
                }
                chunkHeader = ChunkHeader.FromStream(fs);
            }
            return chunkHeader;
        }

        private static ChunkFooter ReadChunkFooter(string chunkFileName)
        {
            ChunkFooter chunkFooter;
            using (var fs = new FileStream(chunkFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (fs.Length < ChunkFooter.Size + ChunkHeader.Size)
                {
                    throw new CorruptDatabaseException(new BadChunkInDatabaseException(
                        string.Format("Chunk file '{0}' is bad. It does not have enough size for header and footer. File size is {1} bytes.",
                                      chunkFileName, fs.Length)));
                }
                fs.Seek(-ChunkFooter.Size, SeekOrigin.End);
                chunkFooter = ChunkFooter.FromStream(fs);
            }
            return chunkFooter;
        }

        private void EnsureNoExcessiveChunks(int lastChunkNum)
        {
            var allowedFiles = new List<string>();
            int cnt = 0;
            for (int i = 0; i <= lastChunkNum; ++i)
            {
                var files = Config.FileNamingStrategy.GetAllVersionsFor(i);
                cnt += files.Length;
                allowedFiles.AddRange(files);
            }

            var allFiles = Config.FileNamingStrategy.GetAllPresentFiles();
            if (allFiles.Length != cnt)
            {
                throw new CorruptDatabaseException(new ExtraneousFileFoundException(
                    string.Format("Unexpected files: {0}.", string.Join(", ", allFiles.Except(allowedFiles)))));
            }
        }

        private void RemoveOldChunksVersions(int lastChunkNum)
        {
            for (int chunkNum = 0; chunkNum <= lastChunkNum; )
            {
                var chunk = Manager.GetChunk(chunkNum);
                for (int i = chunk.ChunkHeader.ChunkStartNumber; i <= chunk.ChunkHeader.ChunkEndNumber; ++i)
                {
                    var files = Config.FileNamingStrategy.GetAllVersionsFor(i);
                    for (int j = (i == chunk.ChunkHeader.ChunkStartNumber ? 1 : 0); j < files.Length; ++j)
                    {
                        RemoveFile("Removing excess chunk version: {0}...", files[j]);
                    }
                }
                chunkNum = chunk.ChunkHeader.ChunkEndNumber + 1;
            }
        }

        private void CleanUpTempFiles()
        {
            var tempFiles = Config.FileNamingStrategy.GetAllTempFiles();
            foreach (string tempFile in tempFiles)
            {
                try
                {
                    RemoveFile("Deleting temporary file {0}...", tempFile);
                }
                catch (Exception exc)
                {
                    Log.ErrorException(exc, "Error while trying to delete remaining temp file: '{0}'.", tempFile);
                }
            }
        }

        private void RemoveFile(string reason, string file)
        {
            Log.Trace(reason, file);
            File.SetAttributes(file, FileAttributes.Normal);
            File.Delete(file);
        }

        public void Dispose()
        {
            Close();
        }

        public void Close()
        {
            if (Manager != null)
                Manager.Dispose();
            Config.WriterCheckpoint.Close();
            Config.ChaserCheckpoint.Close();
            Config.EpochCheckpoint.Close();
            Config.TruncateCheckpoint.Close();
        }
    }
}
