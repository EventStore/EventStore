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
using EventStore.Core.Services;
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

        public TFChunkScavenger(TFChunkDb db, IReadIndex readIndex)
        {
            Ensure.NotNull(db, "db");
            Ensure.NotNull(readIndex, "readIndex");
 
            _db = db;
            _readIndex = readIndex;
        }

        public void Scavenge(bool alwaysKeepScavenged)
        {
            var sw = Stopwatch.StartNew();

            Log.Trace("Started scavenging of DB. Chunks count at start: {0}.", _db.Manager.ChunksCount);

            long scavengePos = 0;
            while (scavengePos < _db.Config.WriterCheckpoint.Read())
            {
                var chunk = _db.Manager.GetChunkFor(scavengePos);
                if (chunk == null)
                    throw new Exception(string.Format("Requested oldChunk for position {0}, which is not present in TFChunkManager.", scavengePos));
                if (!chunk.IsReadOnly)
                {
                    Log.Trace("Stopping scavenging due to non-completed TFChunk for position {0}.", scavengePos);
                    break;
                }
                if (_db.Config.ChaserCheckpoint.Read() < chunk.ChunkHeader.ChunkStartPosition)
                {
                    Log.Trace("Stopping scavenging due to chaser hasn't yet processed TFChunk for position {0} completely.", scavengePos);
                    break;
                }

                ScavengeChunk(chunk, alwaysKeepScavenged);
                scavengePos = chunk.ChunkHeader.ChunkEndPosition;
            }

            Log.Trace("Scavenging pass COMPLETED in {0}.", sw.Elapsed);
        }

        private void ScavengeChunk(TFChunk.TFChunk oldChunk, bool alwaysKeepScavenged)
        {
            var sw = Stopwatch.StartNew();

            int chunkStartNumber = oldChunk.ChunkHeader.ChunkStartNumber;
            int chunkEndNumber = oldChunk.ChunkHeader.ChunkStartNumber;
            long chunkStartPosition = oldChunk.ChunkHeader.ChunkStartPosition;
            int chunkSize = oldChunk.ChunkHeader.ChunkSize;

            var tmpChunkPath = Path.Combine(_db.Config.Path, Guid.NewGuid() + ".scavenge.tmp");
            Log.Trace("Scavenging chunk #{0}-{1} ({2}) started. Temp file: {3}.",
                      chunkStartNumber,
                      chunkEndNumber,
                      Path.GetFileName(oldChunk.FileName),
                      Path.GetFileName(tmpChunkPath));

            TFChunk.TFChunk newChunk;
            try
            {
                newChunk = TFChunk.TFChunk.CreateNew(tmpChunkPath, chunkSize, chunkStartNumber, chunkEndNumber, isScavenged: true);
            }
            catch (IOException exc)
            {
                Log.ErrorException(exc, "IOException during creating new chunk for scavenging purposes. Ignoring...");
                return;
            }

            var commits = new Dictionary<long, CommitInfo>();

            TraverseChunk(oldChunk,
                          prepare => { /* NOOP */ },
                          commit =>
                          {
                              if (commit.TransactionPosition < chunkStartPosition)
                                  return;
                              commits.Add(commit.TransactionPosition, new CommitInfo(commit));
                          },
                          system => { /* NOOP */ });

            var positionMapping = new List<PosMap>();
            TraverseChunk(oldChunk,
                          prepare =>
                          {
                              if (ShouldKeepPrepare(prepare, commits))
                              {
                                  var posMap = WriteRecord(newChunk, prepare);
                                  positionMapping.Add(posMap);
                              }
                          },
                          commit =>
                          {
                              if (ShouldKeepCommit(commit, commits))
                              {
                                  var posMap = WriteRecord(newChunk, commit);
                                  positionMapping.Add(posMap);
                              }
                          },
                          system =>
                          {
                              // we always keep system log records for now
                                var posMap = WriteRecord(newChunk, system);
                                positionMapping.Add(posMap);
                          });

            var oldSize = oldChunk.PhysicalDataSize + oldChunk.ChunkFooter.MapSize + ChunkHeader.Size + ChunkFooter.Size;
            var newSize = newChunk.PhysicalDataSize 
                          + sizeof(ulong) * positionMapping.Count 
                          + ChunkHeader.Size 
                          + ChunkFooter.Size;

            if (!alwaysKeepScavenged && oldSize <= newSize)
            {
                Log.Trace("Scavenging of chunk #{0}-{1} ({2}) completed in {3}.\n"
                          + "Old version is kept as it is smaller.\n"
                          + "Old chunk size: {4}, scavenged size: {5}.\n"
                          + "Scavenged chunk removed.",
                          chunkStartNumber,
                          chunkEndNumber,
                          oldChunk.FileName,
                          sw.Elapsed,
                          oldSize,
                          newSize);

                newChunk.MarkForDeletion();
            }
            else
            {
                newChunk.CompleteScavenge(positionMapping);
                var chunk = _db.Manager.SwitchChunk(newChunk, verifyHash: false, replaceChunksWithGreaterNumbers: false);

                Log.Trace("Scavenging of chunk #{0}-{1} ({2}) completed in {3}.\n"
                          + "File {4} --> {5}.\n" 
                          + "Old size: {6}, new size: {7}.",
                          chunkStartNumber,
                          chunkEndNumber,
                          Path.GetFileName(oldChunk.FileName),
                          sw.Elapsed,
                          Path.GetFileName(tmpChunkPath),
                          Path.GetFileName(chunk.FileName),
                          oldSize,
                          newSize);
            }
        }

        private bool ShouldKeepPrepare(PrepareLogRecord prepare, Dictionary<long, CommitInfo> commits)
        {
            CommitInfo commitInfo;
            if (commits.TryGetValue(prepare.TransactionPosition, out commitInfo))
            {
                //commitInfo.StreamId = prepare.EventStreamId;

                if ((prepare.Flags & PrepareFlags.StreamDelete) != 0                   // we always keep delete tombstones
                    || prepare.EventType.StartsWith(SystemEventTypes.StreamCreated))   // we keep $stream-created
                {
                    commitInfo.KeepCommit = true; // see notes below
                    return true;
                }
                
                if (_readIndex.IsStreamDeleted(prepare.EventStreamId))
                {
                    // When all prepares and commit of transaction belong to single chunk and the stream is deleted,
                    // we can safely delete both prepares and commit.

                    // If someone decided definitely to keep corresponding commit then we shouldn't interfere.
                    // Otherwise we should point that yes, you can remove commit for this prepare.
                    commitInfo.KeepCommit = commitInfo.KeepCommit ?? false; 
                    return false;
                }

                if ((prepare.Flags & PrepareFlags.Data) == 0)
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
                    commitInfo.KeepCommit = commitInfo.KeepCommit ?? false;
                    return false;
                }

                var lastEventNumber = _readIndex.GetLastStreamEventNumber(prepare.EventStreamId);
                var streamMetadata = _readIndex.GetStreamMetadata(prepare.EventStreamId);
                var eventNumber = commitInfo.EventNumber + prepare.TransactionOffset;

                // We should always physically keep the very last prepare in the stream.
                // Otherwise we get into trouble when trying to resolve LastStreamEventNumber, for instance.
                // That is because our TableIndex doesn't keep EventStreamId, only hash of it, so on doing some operations
                // that needs TableIndex, we have to make sure we have prepare records in TFChunks when we need them.
                if (eventNumber >= lastEventNumber)
                {
                    // Definitely keep commit, otherwise current prepare wouldn't be discoverable.
                    // TODO AN I should think more carefully about this stuff with prepare/commits relations
                    // TODO AN and whether we can keep prepare without keeping commit (problems on index rebuild).
                    // TODO AN What can save us -- some effective and clever way to mark prepare as committed, 
                    // TODO AN but place updated prepares in the place of commit, so they are discoverable during 
                    // TODO AN index rebuild or ReadAllForward/Backward queries EXACTLY in the same order as they 
                    // TODO AN were originally written to TF
                    commitInfo.KeepCommit = true; 
                    return true;
                }

                bool keep = true;
                if (streamMetadata.MaxCount.HasValue)
                {
                    int maxKeptEventNumber = lastEventNumber - streamMetadata.MaxCount.Value + 1;
                    if (eventNumber < maxKeptEventNumber)
                        keep = false;
                }

                if (streamMetadata.MaxAge.HasValue)
                {
                    if (prepare.TimeStamp < DateTime.UtcNow - streamMetadata.MaxAge.Value)
                        keep = false;
                }

                if (keep)
                    commitInfo.KeepCommit = true;
                else
                    commitInfo.KeepCommit = commitInfo.KeepCommit ?? false;
                return keep;
            }
            else
            {
                if ((prepare.Flags & PrepareFlags.StreamDelete) != 0                 // we always keep delete tombstones
                    || prepare.EventType.StartsWith(SystemEventTypes.StreamCreated)) // we keep $stream-created
                {
                    return true;
                }

                // So here we have prepare which commit is in the following chunks or prepare is not committed at all.
                // Now, whatever heuristic on prepare scavenge we use, we should never delete the very first prepare
                // in transaction, as in some circumstances we need it.
                // For instance, this prepare could be part of ongoing transaction and though we sometimes can determine
                // that prepare wouldn't ever be needed (e.g., stream was deleted, $maxAge or $maxCount rule it out)
                // we still need the first prepare to find out StreamId for possible commit in StorageWriterService.WriteCommit method. 
                // There could be other reasons where it is needed, so we just safely filter it out to not bother further.
                if ((prepare.Flags & PrepareFlags.TransactionBegin) != 0)
                    return true;

                // If stream of this prepare is deleted, then we can safely delete this prepare.
                if (_readIndex.IsStreamDeleted(prepare.EventStreamId))
                    return false;

                // TODO AN we can try to figure out if this prepare is committed, and if yes, what is its event number.
                // TODO AN only then we can actually do something here, unfortunately.
                return true;

                // We should always physically keep the very last prepare in the stream.
                // Otherwise we get into trouble when trying to resolve LastStreamEventNumber, for instance.
                // That is because our TableIndex doesn't keep EventStreamId, only hash of it, so on doing some operations
                // that needs TableIndex, we have to make sure we have prepare records in TFChunks when we need them.

                /*
                var lastEventNumber = _readIndex.GetLastStreamEventNumber(prepare.EventStreamId);
                var streamMetadata = _readIndex.GetStreamMetadata(prepare.EventStreamId);
                if (streamMetadata.MaxCount.HasValue)
                {
                    // nothing to do here until we know prepare's event number
                }
                if (streamMetadata.MaxAge.HasValue)
                {
                    // nothing to do here until we know prepare's event number
                }
                return false;
                */
            }
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
            // TODO AN: int --> long
            int logPos = (int) newChunk.ChunkHeader.GetChunkLocalLogicalPosition(record.Position);
            int actualPos = (int) writeResult.OldPosition;
            return new PosMap(logPos, actualPos);
        }

        private class CommitInfo
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
}