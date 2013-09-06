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
using System.Linq;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Exceptions;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.Chunks.TFChunk;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.TransactionLog.Chunks
{
    public class TFChunkScavenger
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<TFChunkScavenger>();

        private readonly TFChunkDb _db;
        private readonly IReadIndex _readIndex;
        private readonly long _maxChunkDataSize;

        public TFChunkScavenger(TFChunkDb db, IReadIndex readIndex, long? maxChunkDataSize = null)
        {
            Ensure.NotNull(db, "db");
            Ensure.NotNull(readIndex, "readIndex");
 
            _db = db;
            _readIndex = readIndex;
            _maxChunkDataSize = maxChunkDataSize ?? db.Config.ChunkSize;
        }

        public long Scavenge(bool alwaysKeepScavenged, bool mergeChunks)
        {
            var totalSw = Stopwatch.StartNew();
            var sw = Stopwatch.StartNew();
            long spaceSaved = 0;

            Log.Trace("SCAVENGING: started scavenging of DB. Chunks count at start: {0}. Options: alwaysKeepScavenged = {1}, mergeChunks = {2}", 
                      _db.Manager.ChunksCount, alwaysKeepScavenged, mergeChunks);

            for (long scavengePos = 0; scavengePos < _db.Config.ChaserCheckpoint.Read(); )
            {
                var chunk = _db.Manager.GetChunkFor(scavengePos);
                if (!chunk.IsReadOnly)
                {
                    Log.Trace("SCAVENGING: stopping scavenging pass due to non-completed TFChunk for position {0}.", scavengePos);
                    break;
                }
                
                long saved;
                ScavengeChunks(alwaysKeepScavenged, new[] {chunk}, out saved);
                spaceSaved += saved;

                scavengePos = chunk.ChunkHeader.ChunkEndPosition;
            }
            Log.Trace("SCAVENGING: initial pass completed in {0}.", sw.Elapsed);

            if (mergeChunks)
            {
                bool mergedSomething;
                int passNum = 0;
                do
                {
                    mergedSomething = false;
                    passNum += 1;
                    sw.Restart();

                    var chunks = new List<TFChunk.TFChunk>();
                    long totalDataSize = 0;
                    for (long scavengePos = 0; scavengePos < _db.Config.ChaserCheckpoint.Read();)
                    {
                        var chunk = _db.Manager.GetChunkFor(scavengePos);
                        if (!chunk.IsReadOnly)
                        {
                            Log.Trace("SCAVENGING: stopping scavenging pass due to non-completed TFChunk for position {0}.", scavengePos);
                            break;
                        }
                        if (totalDataSize + chunk.PhysicalDataSize > _maxChunkDataSize)
                        {
                            if (chunks.Count == 0)
                                throw new Exception("SCAVENGING: no chunks to merge, unexpectedly...");
                            long saved;
                            if (chunks.Count > 1 && ScavengeChunks(alwaysKeepScavenged, chunks, out saved))
                            {
                                spaceSaved += saved;
                                mergedSomething = true;
                            }
                            chunks.Clear();
                            totalDataSize = 0;
                        }
                        chunks.Add(chunk);
                        totalDataSize += chunk.PhysicalDataSize;
                        scavengePos = chunk.ChunkHeader.ChunkEndPosition;
                    }
                    if (chunks.Count > 1)
                    {
                        long saved;
                        if (ScavengeChunks(alwaysKeepScavenged, chunks, out saved))
                        {
                            spaceSaved += saved;
                            mergedSomething = true;
                        }
                    }
                    Log.Trace("SCAVENGING: merge pass #{0} completed in {1}. {2} merged.",
                              passNum, sw.Elapsed, mergedSomething ? "Some chunks" : "Nothing");
                } while (mergedSomething);
            }
            Log.Trace("SCAVENGING: total time taken: {0}, total space saved: {1}.", totalSw.Elapsed, spaceSaved);
            return spaceSaved;
        }

        private bool ScavengeChunks(bool alwaysKeepScavenged, IList<TFChunk.TFChunk> oldChunks, out long spaceSaved)
        {
            spaceSaved = 0;

            if (oldChunks.IsEmpty()) throw new ArgumentException("Provided list of chunks to scavenge and merge is empty.");
            
            var sw = Stopwatch.StartNew();

            int chunkStartNumber = oldChunks.First().ChunkHeader.ChunkStartNumber;
            long chunkStartPosition = oldChunks.First().ChunkHeader.ChunkStartPosition;
            int chunkEndNumber = oldChunks.Last().ChunkHeader.ChunkEndNumber;

            var tmpChunkPath = Path.Combine(_db.Config.Path, Guid.NewGuid() + ".scavenge.tmp");
            var oldChunksList = string.Join("\n", oldChunks);
            Log.Trace("SCAVENGING: started to scavenge & merge chunks: {0}\nResulting temp chunk file: {1}.",
                      oldChunksList, Path.GetFileName(tmpChunkPath));

            TFChunk.TFChunk newChunk;
            try
            {
                newChunk = TFChunk.TFChunk.CreateNew(tmpChunkPath, _db.Config.ChunkSize, chunkStartNumber, chunkEndNumber, isScavenged: true);
            }
            catch (IOException exc)
            {
                Log.ErrorException(exc, "IOException during creating new chunk for scavenging purposes. Stopping scavenging process...");
                return false;
            }

            try
            {
                var commits = new Dictionary<long, CommitInfo>();

                foreach (var oldChunk in oldChunks)
                {
                    TraverseChunk(oldChunk,
                                  prepare => { /* NOOP */ },
                                  commit =>
                                  {
                                      if (commit.TransactionPosition >= chunkStartPosition)
                                          commits.Add(commit.TransactionPosition, new CommitInfo(commit));
                                  },
                                  system => { /* NOOP */ });
                }

                var positionMapping = new List<PosMap>();
                foreach (var oldChunk in oldChunks)
                {
                    TraverseChunk(oldChunk,
                                  prepare => 
                                  {
                                      if (ShouldKeepPrepare(prepare, commits))
                                          positionMapping.Add(WriteRecord(newChunk, prepare));
                                  },
                                  commit =>
                                  {
                                      if (ShouldKeepCommit(commit, commits))
                                          positionMapping.Add(WriteRecord(newChunk, commit));
                                  },
                                  // we always keep system log records for now
                                  system => positionMapping.Add(WriteRecord(newChunk, system)));
                }
                newChunk.CompleteScavenge(positionMapping);

                var oldSize = oldChunks.Sum(x => (long)x.PhysicalDataSize + x.ChunkFooter.MapSize + ChunkHeader.Size + ChunkFooter.Size);
                var newSize = (long)newChunk.PhysicalDataSize + PosMap.FullSize * positionMapping.Count + ChunkHeader.Size + ChunkFooter.Size;

                if (oldSize <= newSize && !alwaysKeepScavenged)
                {
                    Log.Trace("Scavenging of chunks:\n{0}\n"
                              + "completed in {1}.\n"
                              + "Old chunks' versions are kept as they are smaller.\n"
                              + "Old chunk total size: {2}, scavenged chunk size: {3}.\n"
                              + "Scavenged chunk removed.", oldChunksList, sw.Elapsed, oldSize, newSize);

                    newChunk.MarkForDeletion();
                    return false;
                }

                var chunk = _db.Manager.SwitchChunk(newChunk, verifyHash: false, removeChunksWithGreaterNumbers: false);
                if (chunk != null)
                {
                    Log.Trace("Scavenging of chunks:\n{0}\n"
                              + "completed in {1}.\n"
                              + "New chunk: {2} --> #{3}-{4} ({5}).\n"
                              + "Old chunks total size: {6}, scavenged chunk size: {7}.",
                              oldChunksList, sw.Elapsed, Path.GetFileName(tmpChunkPath),
                              chunkStartNumber, chunkEndNumber, Path.GetFileName(chunk.FileName), oldSize, newSize);
                    spaceSaved = oldSize - newSize;
                    return true;
                }
                else
                {
                    Log.Trace("Scavenging of chunks:\n{0}\n"
                              + "completed in {1}.\n"
                              + "But switching was prevented for new chunk: #{2}-{3} ({4}).\n"
                              + "Old chunks total size: {5}, scavenged chunk size: {6}.",
                              oldChunksList, sw.Elapsed,
                              chunkStartNumber, chunkEndNumber, Path.GetFileName(tmpChunkPath), oldSize, newSize);
                    return false;
                }
            }
            catch (FileBeingDeletedException exc)
            {
                Log.Info("Got FileBeingDeletedException exception during scavenging, that probably means some chunks were re-replicated.\n"
                         + "Scavenging of following chunks will be skipped:\n{0}\n"
                         + "Stopping scavenging and removing temp chunk '{1}'...\n"
                         + "Exception message: {2}.", oldChunksList, tmpChunkPath, exc.Message);
                Helper.EatException(() =>
                {
                    File.SetAttributes(tmpChunkPath, FileAttributes.Normal);
                    File.Delete(tmpChunkPath);
                });
                return false;
            }
        }

        private bool ShouldKeepPrepare(PrepareLogRecord prepare, Dictionary<long, CommitInfo> commits)
        {
            CommitInfo commitInfo;
            bool isCommitted = commits.TryGetValue(prepare.TransactionPosition, out commitInfo)
                               || prepare.Flags.HasAnyOf(PrepareFlags.IsCommitted);

            if (prepare.Flags.HasAnyOf(PrepareFlags.StreamDelete))
            {
                // We should always keep delete tombstone.
                commitInfo.ForciblyKeep();
                return true;
            }

            if (!isCommitted && prepare.Flags.HasAnyOf(PrepareFlags.TransactionBegin))
            {
                // So here we have prepare which commit is in the following chunks or prepare is not committed at all.
                // Now, whatever heuristic on prepare scavenge we use, we should never delete the very first prepare
                // in transaction, as in some circumstances we need it.
                // For instance, this prepare could be part of ongoing transaction and though we sometimes can determine
                // that prepare wouldn't ever be needed (e.g., stream was deleted, $maxAge or $maxCount rule it out)
                // we still need the first prepare to find out StreamId for possible commit in StorageWriterService.WriteCommit method. 
                // There could be other reasons where it is needed, so we just safely filter it out to not bother further.
                return true;
            }

            var streamInfo = _readIndex.GetStreamInfo(prepare.EventStreamId);
            var lastEventNumber = streamInfo.LastEventNumber;
            if (streamInfo.IsDeleted)
            {
                // When all prepares and commit of transaction belong to single chunk and the stream is deleted,
                // we can safely delete both prepares and commit.
                // Even if this prepare is not committed, but its stream is deleted, then as long as it is
                // not TransactionBegin prepare we can remove it, because any transaction should fail either way on commit stage.
                commitInfo.TryNotToKeep();
                return false;
            }

            if (isCommitted && prepare.Flags.HasNoneOf(PrepareFlags.Data))
            {
                // We encountered system prepare with no data. As of now it can appear only in explicit
                // transactions so we can safely remove it. The performance shouldn't hurt, because
                // TransactionBegin prepare is never needed either way and TransactionEnd should be in most
                // circumstances close to commit, so shouldn't hurt performance too much.
                // The advantage of getting rid of system prepares is ability to completely eliminate transaction 
                // prepares and commit, if transaction events are completely ruled out by $maxAge/$maxCount.
                // Otherwise we'd have to either keep prepare not requiring to keep commit, which could leave 
                // this prepare as never discoverable garbage, or we could insist on keeping commit forever
                // even if all events in transaction are scavenged.
                commitInfo.TryNotToKeep();
                return false;
            }

            if (!isCommitted)
            {
                // If we could somehow figure out (from read index) the event number of this prepare
                // (if it is actually committed, but commit is in another chunk) then we can apply same scavenging logic.
                // Unfortunately, if it is not committed prepare we can say nothing for now, so should conservatively keep it.
                return true;
            }

            var eventNumber = prepare.Flags.HasAnyOf(PrepareFlags.IsCommitted)
                                      ? prepare.ExpectedVersion + 1 // IsCommitted prepares always have explicit expected version
                                      : commitInfo.EventNumber + prepare.TransactionOffset;

            // We should always physically keep the very last prepare in the stream.
            // Otherwise we get into trouble when trying to resolve LastStreamEventNumber, for instance.
            // That is because our TableIndex doesn't keep EventStreamId, only hash of it, so on doing some operations
            // that needs TableIndex, we have to make sure we have prepare records in TFChunks when we need them.
            if (eventNumber >= lastEventNumber)
            {
                // Definitely keep commit, otherwise current prepare wouldn't be discoverable.
                commitInfo.ForciblyKeep();
                return true;
            }

            var meta = _readIndex.GetStreamMetadata(prepare.EventStreamId);
            bool canRemove = (meta.MaxCount.HasValue && eventNumber < lastEventNumber - meta.MaxCount.Value + 1)
                          || (meta.StartFrom.HasValue && eventNumber < meta.StartFrom.Value)
                          || (meta.MaxAge.HasValue && prepare.TimeStamp < DateTime.UtcNow - meta.MaxAge.Value);

            if (canRemove)
                commitInfo.TryNotToKeep();
            else
                commitInfo.ForciblyKeep();
            return !canRemove;
        }

        private bool ShouldKeepCommit(CommitLogRecord commit, Dictionary<long, CommitInfo> commits)
        {
            CommitInfo commitInfo;
            if (commits.TryGetValue(commit.TransactionPosition, out commitInfo))
                return commitInfo.KeepCommit != false;
            return true;
        }

        private void TraverseChunk(TFChunk.TFChunk chunk, 
                                   Action<PrepareLogRecord> processPrepare, 
                                   Action<CommitLogRecord> processCommit,
                                   Action<SystemLogRecord> processSystem)
        {
            var result = chunk.TryReadFirst();
            while (result.Success)
            {
                var record = result.LogRecord;
                switch (record.RecordType)
                {
                    case LogRecordType.Prepare:
                    {
                        var prepare = (PrepareLogRecord)record;
                        processPrepare(prepare);
                        break;
                    }
                    case LogRecordType.Commit:
                    {
                        var commit = (CommitLogRecord)record;
                        processCommit(commit);
                        break;
                    }
                    case LogRecordType.System:
                    {
                        var system = (SystemLogRecord)record;
                        processSystem(system);
                        break;
                    }
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                result = chunk.TryReadClosestForward((int)result.NextPosition);
            }
        }

        private static PosMap WriteRecord(TFChunk.TFChunk newChunk, LogRecord record)
        {
            var writeResult = newChunk.TryAppend(record);
            if (!writeResult.Success)
            {
                throw new Exception(string.Format(
                        "Unable to append record during scavenging. Scavenge position: {0}, Record: {1}.",
                        writeResult.OldPosition,
                        record));
            }
            long logPos = newChunk.ChunkHeader.GetLocalLogPosition(record.LogPosition);
            int actualPos = (int) writeResult.OldPosition;
            return new PosMap(logPos, actualPos);
        }

        internal class CommitInfo
        {
            public readonly int EventNumber;

            //public string StreamId;
            public bool? KeepCommit;

            public CommitInfo(CommitLogRecord commitRecord)
            {
                EventNumber = commitRecord.FirstEventNumber;
            }

            public override string ToString()
            {
                return string.Format("EventNumber: {0}, KeepCommit: {1}", EventNumber, KeepCommit);
            }
        }
    }

    internal static class CommitInfoExtensions
    {
        public static void ForciblyKeep(this TFChunkScavenger.CommitInfo commitInfo)
        {
            if (commitInfo != null)
                commitInfo.KeepCommit = true;
        }

        public static void TryNotToKeep(this TFChunkScavenger.CommitInfo commitInfo)
        {
            // If someone decided definitely to keep corresponding commit then we shouldn't interfere.
            // Otherwise we should point that yes, you can remove commit for this prepare.
            if (commitInfo != null)
                commitInfo.KeepCommit = commitInfo.KeepCommit ?? false;
        }
    }
}