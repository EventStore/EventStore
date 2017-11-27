using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Exceptions;
using EventStore.Core.Helpers;
using EventStore.Core.Index;
using EventStore.Core.Services;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Services.UserManagement;
using EventStore.Core.TransactionLog.Chunks.TFChunk;
using EventStore.Core.TransactionLog.LogRecords;
using EventStore.Core.Messages;

namespace EventStore.Core.TransactionLog.Chunks
{
    public class TFChunkScavenger
    {
        private static readonly ILogger Log = LogManager.GetLoggerFor<TFChunkScavenger>();

        private readonly TFChunkDb _db;
        private readonly IODispatcher _ioDispatcher;
        private readonly ITableIndex _tableIndex;
        private readonly Guid _scavengeId;
        private readonly string _nodeEndpoint;
        private readonly IReadIndex _readIndex;
        private readonly long _maxChunkDataSize;
        private readonly bool _unsafeIgnoreHardDeletes;
        private const int MaxRetryCount = 5;

        public TFChunkScavenger(TFChunkDb db, IODispatcher ioDispatcher, ITableIndex tableIndex, IReadIndex readIndex,
                                Guid scavengeId, string nodeEndpoint, long? maxChunkDataSize = null, bool unsafeIgnoreHardDeletes=false)
        {
            Ensure.NotNull(db, "db");
            Ensure.NotNull(ioDispatcher, "ioDispatcher");
            Ensure.NotNull(tableIndex, "tableIndex");
            Ensure.NotNull(nodeEndpoint, "nodeEndpoint");
            Ensure.NotNull(readIndex, "readIndex");

            _db = db;
            _ioDispatcher = ioDispatcher;
            _tableIndex = tableIndex;
            _scavengeId = scavengeId;
            _nodeEndpoint = nodeEndpoint;
            _readIndex = readIndex;
            _maxChunkDataSize = maxChunkDataSize ?? db.Config.ChunkSize;
            _unsafeIgnoreHardDeletes = unsafeIgnoreHardDeletes;
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
            long chunkStartPos = oldChunks.First().ChunkHeader.ChunkStartPosition;
            int chunkEndNumber = oldChunks.Last().ChunkHeader.ChunkEndNumber;
            long chunkEndPos = oldChunks.Last().ChunkHeader.ChunkEndPosition;

            var tmpChunkPath = Path.Combine(_db.Config.Path, Guid.NewGuid() + ".scavenge.tmp");
            var oldChunksList = string.Join("\n", oldChunks);
            Log.Trace("SCAVENGING: started to scavenge & merge chunks: {0}", oldChunksList);
            Log.Trace("Resulting temp chunk file: {0}.", Path.GetFileName(tmpChunkPath));

            TFChunk.TFChunk newChunk;
            try
            {
                newChunk = TFChunk.TFChunk.CreateNew(tmpChunkPath,
                                                     _db.Config.ChunkSize,
                                                     chunkStartNumber,
                                                     chunkEndNumber,
                                                     isScavenged: true,
                                                     inMem: _db.Config.InMemDb,
                                                     unbuffered: _db.Config.Unbuffered,
                                                     writethrough: _db.Config.WriteThrough,
                                                     initialReaderCount: _db.Config.InitialReaderCount);
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
                                  (prepare, _) => { /* NOOP */ },
                                  (commit, _) =>
                                  {
                                      if (commit.TransactionPosition >= chunkStartPos)
                                          commits.Add(commit.TransactionPosition, new CommitInfo(commit));
                                  },
                                  (system, _) => { /* NOOP */ });
                }

                long newSize = 0;
                int positionMapCount = 0;

                foreach (var oldChunk in oldChunks)
                {
                    TraverseChunk(oldChunk,
                                  (prepare, len) =>
                                  {
                                      if (ShouldKeepPrepare(prepare, commits, chunkStartPos, chunkEndPos))
                                      {
                                          newSize += len + 2 * sizeof(int);
                                          positionMapCount++;
                                      }
                                  },
                                  (commit, len) =>
                                  {
                                      if (ShouldKeepCommit(commit, commits))
                                      {
                                          newSize += len + 2 * sizeof(int);
                                          positionMapCount++;
                                      }
                                  },
                                  (system, len) =>
                                  {
                                      newSize += len + 2 * sizeof(int);
                                      positionMapCount++;
                                  });
                }

                newSize += positionMapCount * PosMap.FullSize + ChunkHeader.Size + ChunkFooter.Size;

                var oldVersion = oldChunks.Any(x => x.ChunkHeader.Version != 3);
                var oldSize = oldChunks.Sum(x => (long)x.PhysicalDataSize + x.ChunkFooter.MapSize + ChunkHeader.Size + ChunkFooter.Size);

                if (oldSize <= newSize && !alwaysKeepScavenged && !_unsafeIgnoreHardDeletes && !oldVersion)
                {
                    Log.Trace("Scavenging of chunks:");
                    Log.Trace(oldChunksList);
                    Log.Trace("completed in {0}.", sw.Elapsed);
                    Log.Trace("Old chunks' versions are kept as they are smaller.");
                    Log.Trace("Old chunk total size: {0}, scavenged chunk size: {1}.", oldSize, newSize);
                    Log.Trace("Scavenged chunk removed.");

                    newChunk.MarkForDeletion();
                    PublishChunksCompletedEvent(chunkStartNumber, chunkEndNumber, sw.Elapsed, false, spaceSaved);
                    return false;
                }

                var positionMapping = new List<PosMap>();
                foreach (var oldChunk in oldChunks)
                {
                    TraverseChunk(oldChunk,
                                  (prepare, _) =>
                                  {
                                      if (ShouldKeepPrepare(prepare, commits, chunkStartPos, chunkEndPos))
                                          positionMapping.Add(WriteRecord(newChunk, prepare));
                                  },
                                  (commit, _) =>
                                  {
                                      if (ShouldKeepCommit(commit, commits))
                                          positionMapping.Add(WriteRecord(newChunk, commit));
                                  },
                                  // we always keep system log records for now
                                  (system, _) => positionMapping.Add(WriteRecord(newChunk, system)));
                }
                newChunk.CompleteScavenge(positionMapping);

                if(_unsafeIgnoreHardDeletes) {
                    Log.Trace("Forcing scavenge chunk to be kept even if bigger.");
                }

                if(oldVersion) {
                    Log.Trace("Forcing scavenged chunk to be kept as old chunk is a previous version.");
                }

                var chunk = _db.Manager.SwitchChunk(newChunk, verifyHash: false, removeChunksWithGreaterNumbers: false);
                if (chunk != null)
                {
                    Log.Trace("Scavenging of chunks:");
                    Log.Trace(oldChunksList);
                    Log.Trace("completed in {0}.", sw.Elapsed);
                    Log.Trace("New chunk: {0} --> #{1}-{2} ({3}).", Path.GetFileName(tmpChunkPath), chunkStartNumber,
                        chunkEndNumber, Path.GetFileName(chunk.FileName));
                    Log.Trace("Old chunks total size: {0}, scavenged chunk size: {1}.", oldSize, newSize);
                    spaceSaved = oldSize - newSize;
                    PublishChunksCompletedEvent(chunkStartNumber, chunkEndNumber, sw.Elapsed, true, spaceSaved);
                    return true;
                }
                else
                {
                    Log.Trace("Scavenging of chunks:");
                    Log.Trace("{0}", oldChunksList);
                    Log.Trace("completed in {1}.", sw.Elapsed);
                    Log.Trace("But switching was prevented for new chunk: #{0}-{1} ({2}).", chunkStartNumber, chunkEndNumber, Path.GetFileName(tmpChunkPath));
                    Log.Trace("Old chunks total size: {0}, scavenged chunk size: {1}.", oldSize, newSize);
                    PublishChunksCompletedEvent(chunkStartNumber, chunkEndNumber, sw.Elapsed, false, spaceSaved);
                    return false;
                }
            }
            catch (FileBeingDeletedException exc)
            {
                Log.Info("Got FileBeingDeletedException exception during scavenging, that probably means some chunks were re-replicated.");
                Log.Info("Scavenging of following chunks will be skipped:");
                Log.Info("{0}", oldChunksList);
                Log.Info("Stopping scavenging and removing temp chunk '{0}'...", tmpChunkPath);
                Log.Info("Exception message: {0}.", exc.Message);
                DeleteTempChunk(tmpChunkPath, MaxRetryCount);
                PublishChunksCompletedEvent(chunkStartNumber, chunkEndNumber, sw.Elapsed, false, spaceSaved, exc.Message);
                return false;
            }
            catch (Exception ex)
            {
                Log.Info("Got exception while scavenging chunk: #{0}-{1}. This chunk will be skipped\n"
                         + "Exception: {2}.", chunkStartNumber, chunkEndNumber, ex.ToString());
                DeleteTempChunk(tmpChunkPath, MaxRetryCount);
                PublishChunksCompletedEvent(chunkStartNumber, chunkEndNumber, sw.Elapsed, false, 0, ex.Message);
                return false;
            }
        }

        private void DeleteTempChunk(string tmpChunkPath, int retries)
        {
            try
            {
                File.SetAttributes(tmpChunkPath, FileAttributes.Normal);
                File.Delete(tmpChunkPath);
            }
            catch(Exception ex)
            {
                if (retries > 0) {
                    Log.Error("Failed to delete the temp chunk. Retrying {0}/{1}. Reason: {2}",
                        MaxRetryCount - retries, MaxRetryCount, ex);
                    DeleteTempChunk(tmpChunkPath, retries - 1);
                }
                else
                {
                    Log.Error("Failed to delete the temp chunk. Retry limit of {0} reached. Reason: {1}", MaxRetryCount, ex);
                    throw;
                }
            }
        }

        private void PublishChunksCompletedEvent(int chunkStartNumber, int chunkEndNumber,
                                                 TimeSpan elapsed, bool wasScavenged, long spaceSaved, string errorMessage = "")
        {
            var evnt = new Event(Guid.NewGuid(), SystemEventTypes.ScavengeChunksCompleted, true, new Dictionary<string, object>{
                    {"scavengeId", _scavengeId},
                    {"chunkStartNumber", chunkStartNumber},
                    {"chunkEndNumber", chunkEndNumber},
                    {"timeTaken", elapsed},
                    {"wasScavenged", wasScavenged},
                    {"spaceSaved", spaceSaved},
                    {"nodeEndpoint", _nodeEndpoint},
                    {"errorMessage", errorMessage}
                }.ToJsonBytes(), null);

            var streamName = string.Format("{0}-{1}", SystemStreams.ScavengesStream, _scavengeId);
            WriteScavengeChunkCompletedEvent(streamName, evnt, MaxRetryCount);
        }

        private void WriteScavengeChunkCompletedEvent(string streamId, Event eventToWrite, int retryCount){
            _ioDispatcher.WriteEvent(streamId, ExpectedVersion.Any, eventToWrite, SystemAccount.Principal, m => WriteScavengeChunkCompletedEventCompleted(m, streamId, eventToWrite, retryCount));
        }

        private void WriteScavengeChunkCompletedEventCompleted(ClientMessage.WriteEventsCompleted msg, string streamId, Event eventToWrite, int retryCount){
            if(msg.Result != OperationResult.Success){
                if(retryCount > 0){
                    WriteScavengeChunkCompletedEvent(streamId, eventToWrite, --retryCount);
                }else{
                    Log.Error("Failed to write an event to the {0} stream. Retry limit of {1} reached. Reason: {2}", streamId, MaxRetryCount, msg.Result);
                }
            }
        }

        private bool ShouldKeepPrepare(PrepareLogRecord prepare, Dictionary<long, CommitInfo> commits, long chunkStart, long chunkEnd)
        {
            CommitInfo commitInfo;
            bool isCommitted = commits.TryGetValue(prepare.TransactionPosition, out commitInfo)
                               || prepare.Flags.HasAnyOf(PrepareFlags.IsCommitted);

            if (prepare.Flags.HasAnyOf(PrepareFlags.StreamDelete))
            {
                if(_unsafeIgnoreHardDeletes) {
                    Log.Info("Removing hard deleted stream tombstone for stream {0} at position {1}", prepare.EventStreamId, prepare.TransactionPosition);
                    commitInfo.TryNotToKeep();
                } else {
                    commitInfo.ForciblyKeep();
                }
                return !_unsafeIgnoreHardDeletes;
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

            var lastEventNumber = _readIndex.GetStreamLastEventNumber(prepare.EventStreamId);
            if (lastEventNumber == EventNumber.DeletedStream)
            {
                // When all prepares and commit of transaction belong to single chunk and the stream is deleted,
                // we can safely delete both prepares and commit.
                // Even if this prepare is not committed, but its stream is deleted, then as long as it is
                // not TransactionBegin prepare we can remove it, because any transaction should fail either way on commit stage.
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

            if (prepare.Flags.HasNoneOf(PrepareFlags.Data))
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

            if (IsSoftDeletedTempStreamWithinSameChunk(prepare.EventStreamId, chunkStart, chunkEnd))
            {
                commitInfo.TryNotToKeep();
                return false;
            }

            var eventNumber = prepare.Flags.HasAnyOf(PrepareFlags.IsCommitted)
                                      ? prepare.ExpectedVersion + 1 // IsCommitted prepares always have explicit expected version
                                      : commitInfo.EventNumber + prepare.TransactionOffset;

            if (!KeepOnlyFirstEventOfDuplicate(_tableIndex, prepare, eventNumber)){
                commitInfo.TryNotToKeep();
                return false;
            }

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
                          || (meta.TruncateBefore.HasValue && eventNumber < meta.TruncateBefore.Value)
                          || (meta.MaxAge.HasValue && prepare.TimeStamp < DateTime.UtcNow - meta.MaxAge.Value);

            if (canRemove)
                commitInfo.TryNotToKeep();
            else
                commitInfo.ForciblyKeep();
            return !canRemove;
        }

        private bool KeepOnlyFirstEventOfDuplicate(ITableIndex tableIndex, PrepareLogRecord prepare, long eventNumber){
            var result = _readIndex.ReadEvent(prepare.EventStreamId, eventNumber);
            if(result.Result == ReadEventResult.Success && result.Record.LogPosition != prepare.LogPosition) return false;
            return true;
        }

        private bool IsSoftDeletedTempStreamWithinSameChunk(string eventStreamId, long chunkStart, long chunkEnd)
        {
            string sh;
            string msh;
            if (SystemStreams.IsMetastream(eventStreamId))
            {
                var originalStreamId = SystemStreams.OriginalStreamOf(eventStreamId);
                var meta = _readIndex.GetStreamMetadata(originalStreamId);
                if (meta.TruncateBefore != EventNumber.DeletedStream || meta.TempStream != true)
                    return false;
                sh = originalStreamId;
                msh = eventStreamId;
            }
            else
            {
                var meta = _readIndex.GetStreamMetadata(eventStreamId);
                if (meta.TruncateBefore != EventNumber.DeletedStream || meta.TempStream != true)
                    return false;
                sh = eventStreamId;
                msh = SystemStreams.MetastreamOf(eventStreamId);
            }

            IndexEntry e;
            var allInChunk = _tableIndex.TryGetOldestEntry(sh, out e) && e.Position >= chunkStart && e.Position < chunkEnd
                          && _tableIndex.TryGetLatestEntry(sh, out e) && e.Position >= chunkStart && e.Position < chunkEnd
                          && _tableIndex.TryGetOldestEntry(msh, out e) && e.Position >= chunkStart && e.Position < chunkEnd
                          && _tableIndex.TryGetLatestEntry(msh, out e) && e.Position >= chunkStart && e.Position < chunkEnd;
            return allInChunk;
        }

        private bool ShouldKeepCommit(CommitLogRecord commit, Dictionary<long, CommitInfo> commits)
        {
            CommitInfo commitInfo;
            if (commits.TryGetValue(commit.TransactionPosition, out commitInfo))
                return commitInfo.KeepCommit != false;
            return true;
        }

        private void TraverseChunk(TFChunk.TFChunk chunk,
                                   Action<PrepareLogRecord, int> processPrepare,
                                   Action<CommitLogRecord, int> processCommit,
                                   Action<SystemLogRecord, int> processSystem)
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
                        processPrepare(prepare, result.RecordLength);
                        break;
                    }
                    case LogRecordType.Commit:
                    {
                        var commit = (CommitLogRecord)record;
                        processCommit(commit, result.RecordLength);
                        break;
                    }
                    case LogRecordType.System:
                    {
                        var system = (SystemLogRecord)record;
                        processSystem(system, result.RecordLength);
                        break;
                    }
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                result = chunk.TryReadClosestForward(result.NextPosition);
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
            public readonly long EventNumber;

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