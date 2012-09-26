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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.TransactionLog.Chunks
{
    public class TFChunkScavenger
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<TFChunkScavenger>();

        private readonly TFChunkDb _db;
        private readonly IReadIndex _readIndex;

        public TFChunkScavenger(TFChunkDb db, IReadIndex readIndex)
        {
            _db = db;
            _readIndex = readIndex;
            Ensure.NotNull(db, "db");
            Ensure.NotNull(readIndex, "readIndex");
        }

        public void Scavenge()
        {
            var sw = Stopwatch.StartNew();

            Log.Trace("Started scavenging of DB. Chunks count at start: {0}.", _db.Manager.ChunksCount);

            for (int i = 0; i < _db.Manager.ChunksCount; ++i)
            {
                var chunk = _db.Manager.GetChunk(i);
                if (chunk == null)
                    throw new Exception(string.Format("Requested oldChunk #{0}, which is not present in TFChunkManager.", i));
                if (!chunk.IsReadOnly)
                {
                    Log.Trace("Stopping scavenging due to non-completed TFChunk #{0}.", i);
                    break;
                }
                ScavengeChunk(chunk);
            }

            Log.Trace("Scavenging pass COMPLETED in {0}.", sw.Elapsed);
        }

        private void ScavengeChunk(TFChunk oldChunk)
        {
            var sw = Stopwatch.StartNew();

            var chunkNumber = oldChunk.ChunkHeader.ChunkStartNumber;
            var newScavengeVersion = oldChunk.ChunkHeader.ChunkScavengeVersion + 1;
            var chunkSize = oldChunk.ChunkHeader.ChunkSize;

            var tmpChunkPath = Path.Combine(_db.Config.Path, Guid.NewGuid() + ".scavenge.tmp");
            var newChunkPath = _db.Config.FileNamingStrategy.GetFilenameFor(chunkNumber, newScavengeVersion);
            Log.Trace("Scavenging chunk #{0} ({1}) started. Scavenged chunk: {2} --> {3}.",
                      chunkNumber,
                      Path.GetFileName(oldChunk.FileName),
                      Path.GetFileName(tmpChunkPath),
                      Path.GetFileName(newChunkPath));

            TFChunk newChunk;
            try
            {
                newChunk = TFChunk.CreateNew(tmpChunkPath, chunkSize, chunkNumber, newScavengeVersion);
            }
            catch (IOException exc)
            {
                Log.ErrorException(exc, "IOException during creating new chunk for scavenging purposes. Ignoring...");
                return;
            }

            var positionMapping = new List<PosMap>();
            var positioningNeeded = false;
            var result = oldChunk.TryReadFirst();
            int cnt = 0;
            while (result.Success)
            {
                cnt += 1;
                var record = result.LogRecord;
                switch (record.RecordType)
                {
                    case LogRecordType.Prepare:
                    {
                        var prepare = (PrepareLogRecord) record;

                        if (!_readIndex.IsStreamDeleted(prepare.EventStreamId)
                            || (prepare.Flags & PrepareFlags.StreamDelete) != 0) // delete tombstone should be left
                        {
                            var posMap = WriteRecord(newChunk, record);
                            positionMapping.Add(posMap);
                            positioningNeeded = posMap.LogPos != posMap.ActualPos;
                        }
                        break;
                    }
                    case LogRecordType.Commit:
                    {
                        var posMap = WriteRecord(newChunk, record);
                        positionMapping.Add(posMap);
                        positioningNeeded = posMap.LogPos != posMap.ActualPos;
                        break;
                    }
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                if (result.NextPosition == -1)
                    break;
                result = oldChunk.TryReadSameOrClosest((int)result.NextPosition);
            }

            var oldSize = oldChunk.ChunkFooter.ActualChunkSize + oldChunk.ChunkFooter.MapSize + ChunkHeader.Size + ChunkFooter.Size;
            var newSize = newChunk.ActualDataSize 
                          + (positioningNeeded ? sizeof(ulong) * positionMapping.Count : 0) 
                          + ChunkHeader.Size 
                          + ChunkFooter.Size;

            if (false && oldSize <= newSize)
            {
                Log.Trace("Scavenging of chunk #{0} ({1}) completed in {2}.\n"
                          + "Old version is kept as it is smaller.\n"
                          + "Old chunk size: {3}, scavenged size: {4}.\n"
                          + "Scavenged chunk removed.",
                          chunkNumber,
                          oldChunk.FileName,
                          sw.Elapsed,
                          oldSize,
                          newSize);

                newChunk.Dispose();
                File.Delete(newChunk.FileName);
            }
            else
            {
                newChunk.CompleteScavenge(positioningNeeded ? positionMapping : null);
                newChunk.Dispose();

                File.Move(tmpChunkPath, newChunkPath);

                newChunk = TFChunk.FromCompletedFile(newChunkPath);
                var removedChunk = _db.Manager.SwapChunk(chunkNumber, newChunk);
                Debug.Assert(ReferenceEquals(removedChunk, oldChunk)); // only scavenging could switch, so old should be always same
                oldChunk.MarkForDeletion();

                Log.Trace("Scavenging of chunk #{0} ({1}) completed in {2} into ({3} --> {4}).\n" 
                          + "Old size: {5}, new size: {6}, new scavenge version: {7}.",
                          chunkNumber,
                          Path.GetFileName(oldChunk.FileName),
                          sw.Elapsed,
                          Path.GetFileName(tmpChunkPath),
                          Path.GetFileName(newChunkPath),
                          oldSize,
                          newSize,
                          newScavengeVersion);
            }
        }

        private static PosMap WriteRecord(TFChunk newChunk, LogRecord record)
        {
            var writeResult = newChunk.TryAppend(record);
            if (!writeResult.Success)
            {
                throw new Exception(string.Format(
                        "Unable to append record during scavenging. Scavenge position: {0}, Record: {1}.",
                        writeResult.OldPosition,
                        record));
            }
            int logPos = (int) (record.Position%newChunk.ChunkHeader.ChunkSize);
            int actualPos = (int) writeResult.OldPosition;
            return new PosMap(logPos, actualPos);
        }
    }
}