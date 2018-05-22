using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Exceptions;
using EventStore.Core.Index;
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
        private readonly ITFChunkScavengerLog _scavengerLog;
        private readonly ITableIndex _tableIndex;
        private readonly IReadIndex _readIndex;
        private readonly long _maxChunkDataSize;
        private readonly bool _unsafeIgnoreHardDeletes;
        private readonly int _threads;
        private const int MaxRetryCount = 5;

        public TFChunkScavenger(TFChunkDb db, ITFChunkScavengerLog scavengerLog, ITableIndex tableIndex, IReadIndex readIndex, long? maxChunkDataSize = null,
            bool unsafeIgnoreHardDeletes = false, int threads = 1)
        {
            Ensure.NotNull(db, "db");
            Ensure.NotNull(scavengerLog, "scavengerLog");
            Ensure.NotNull(tableIndex, "tableIndex");
            Ensure.NotNull(readIndex, "readIndex");

            _db = db;
            _scavengerLog = scavengerLog;
            _tableIndex = tableIndex;
            _readIndex = readIndex;
            _maxChunkDataSize = maxChunkDataSize ?? db.Config.ChunkSize;
            _unsafeIgnoreHardDeletes = unsafeIgnoreHardDeletes;
            _threads = threads;
        }

        public string ScavengeId => _scavengerLog.ScavengeId;

        private IEnumerable<TFChunk.TFChunk> GetAllChunks(int startFromChunk)
        {
            long scavengePos = _db.Config.ChunkSize * (long)startFromChunk;
            while (scavengePos < _db.Config.ChaserCheckpoint.Read())
            {
                var chunk = _db.Manager.GetChunkFor(scavengePos);
                if (!chunk.IsReadOnly)
                {
                    yield break;
                }

                yield return chunk;

                scavengePos = chunk.ChunkHeader.ChunkEndPosition;
            }
        }

        public Task Scavenge(bool alwaysKeepScavenged, bool mergeChunks, int startFromChunk = 0, CancellationToken ct = default(CancellationToken))
        {
            Ensure.Nonnegative(startFromChunk, nameof(startFromChunk));

            // Note we aren't passing the CancellationToken to the task on purpose so awaiters
            // don't have to handle Exceptions and can wait for the actual completion of the task.
            return Task.Factory.StartNew(() =>
            {
                var sw = Stopwatch.StartNew();

                ScavengeResult result = ScavengeResult.Success;
                string error = null;
                try
                {
                    _scavengerLog.ScavengeStarted();

                    ScavengeInternal(alwaysKeepScavenged, mergeChunks, startFromChunk, ct);
                }
                catch (OperationCanceledException)
                {
                    Log.Info("SCAVENGING: Scavenge cancelled.");
                    result = ScavengeResult.Stopped;
                }
                catch (Exception exc)
                {
                    result = ScavengeResult.Failed;
                    Log.ErrorException(exc, "SCAVENGING: error while scavenging DB.");
                    error = string.Format("Error while scavenging DB: {0}.", exc.Message);
                }
                finally
                {
                    try
                    {
                        _scavengerLog.ScavengeCompleted(result, error, sw.Elapsed);
                    }
                    catch (Exception ex)
                    {
                        Log.ErrorException(ex, "Error whilst recording scavenge completed. Scavenge result: {0}, Elapsed: {1}, Original error: {2}", result, sw.Elapsed, error);
                    }
                }
            }, TaskCreationOptions.LongRunning);
        }

        private void ScavengeInternal(bool alwaysKeepScavenged, bool mergeChunks, int startFromChunk, CancellationToken ct)
        {
            var totalSw = Stopwatch.StartNew();
            var sw = Stopwatch.StartNew();

            Log.Trace("SCAVENGING: started scavenging of DB. Chunks count at start: {0}. Options: alwaysKeepScavenged = {1}, mergeChunks = {2}",
                _db.Manager.ChunksCount, alwaysKeepScavenged, mergeChunks);
            
            // Initial scavenge pass
            var chunksToScavenge = GetAllChunks(startFromChunk);

            Parallel.ForEach(chunksToScavenge,
                new ParallelOptions {MaxDegreeOfParallelism = _threads, CancellationToken = ct}, (chunk, pls) => ScavengeChunk(alwaysKeepScavenged, chunk, ct));

            Log.Trace("SCAVENGING: initial pass completed in {0}.", sw.Elapsed);

            // Merge scavenge pass
            if (mergeChunks)
            {
                bool mergedSomething;
                int passNum = 0;
                do
                {
                    mergedSomething = false;
                    passNum += 1;
                    sw.Restart();

                    var chunksToMerge = new List<TFChunk.TFChunk>();
                    long totalDataSize = 0;
                    foreach (var chunk in GetAllChunks(startFromChunk))
                    {
                        ct.ThrowIfCancellationRequested();

                        if (totalDataSize + chunk.PhysicalDataSize > _maxChunkDataSize)
                        {
                            if (chunksToMerge.Count == 0)
                                throw new Exception("SCAVENGING: no chunks to merge, unexpectedly...");

                            if (chunksToMerge.Count > 1 && MergeChunks(chunksToMerge, ct))
                            {
                                mergedSomething = true;
                            }

                            chunksToMerge.Clear();
                            totalDataSize = 0;
                        }

                        chunksToMerge.Add(chunk);
                        totalDataSize += chunk.PhysicalDataSize;
                    }

                    if (chunksToMerge.Count > 1)
                    {
                        if (MergeChunks(chunksToMerge, ct))
                        {
                            mergedSomething = true;
                        }
                    }

                    Log.Trace("SCAVENGING: merge pass #{0} completed in {1}. {2} merged.",
                        passNum, sw.Elapsed, mergedSomething ? "Some chunks" : "Nothing");
                } while (mergedSomething);
            }

            Log.Trace("SCAVENGING: total time taken: {0}.", totalSw.Elapsed);
        }

        private bool ShouldKeep(RecordReadResult result, Dictionary<long, CommitInfo> commits, long chunkStartPos, long chunkEndPos)
        {
            switch (result.LogRecord.RecordType)
            {
                case LogRecordType.Prepare:
                    var prepare = (PrepareLogRecord) result.LogRecord;
                    if (ShouldKeepPrepare(prepare, commits, chunkStartPos, chunkEndPos))
                        return true;
                    break;
                case LogRecordType.Commit:
                    var commit = (CommitLogRecord) result.LogRecord;
                    if (ShouldKeepCommit(commit, commits))
                        return true;
                    break;
                case LogRecordType.System:
                    return true;
            }

            return false;
        }

        private void ScavengeChunk(bool alwaysKeepScavenged, TFChunk.TFChunk oldChunk, CancellationToken ct)
        {
            if (oldChunk == null) throw new ArgumentNullException("oldChunk");

            var sw = Stopwatch.StartNew();

            int chunkStartNumber = oldChunk.ChunkHeader.ChunkStartNumber;
            long chunkStartPos = oldChunk.ChunkHeader.ChunkStartPosition;
            int chunkEndNumber = oldChunk.ChunkHeader.ChunkEndNumber;
            long chunkEndPos = oldChunk.ChunkHeader.ChunkEndPosition;

            var tmpChunkPath = Path.Combine(_db.Config.Path, Guid.NewGuid() + ".scavenge.tmp");
            var oldChunkName = oldChunk.ToString();
            Log.Trace("SCAVENGING: started to scavenge chunks: {0} {1} => {2} ({3} => {4})", oldChunkName, chunkStartNumber, chunkEndNumber,
                chunkStartPos, chunkEndPos);
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
                    initialReaderCount: _db.Config.InitialReaderCount,
                    reduceFileCachePressure: _db.Config.ReduceFileCachePressure);
            }
            catch (IOException exc)
            {
                Log.ErrorException(exc, "IOException during creating new chunk for scavenging purposes. Stopping scavenging process...");
                throw;
            }

            try
            {
                var commits = new Dictionary<long, CommitInfo>();

                int count = TraverseChunkBasic(oldChunk, ct,
                    (result, i) =>
                    {
                        ct.ThrowIfCancellationRequested();

                        if (result.LogRecord.RecordType == LogRecordType.Commit)
                        {
                            var commit = (CommitLogRecord) result.LogRecord;
                            if (commit.TransactionPosition >= chunkStartPos)
                                commits.Add(commit.TransactionPosition, new CommitInfo(commit));
                        }
                    });


                long newSize = 0;
                BitArray filteredIndexes = new BitArray(count);
                int filteredCount = 0;

                TraverseChunkBasic(oldChunk, ct,
                    (result, i) =>
                    {
                        if (ShouldKeep(result, commits, chunkStartPos, chunkEndPos))
                        {
                            newSize += result.RecordLength + 2 * sizeof(int);

                            filteredCount++;
                            filteredIndexes[i] = true;
                        }
                    });

                Log.Trace("Scavenging {0} traversed {1} including {2}.", oldChunkName, count, filteredCount);

                newSize += filteredCount * PosMap.FullSize + ChunkHeader.Size + ChunkFooter.Size;
                if (newChunk.ChunkHeader.Version >= (byte) TFChunk.TFChunk.ChunkVersions.Aligned)
                    newSize = TFChunk.TFChunk.GetAlignedSize((int) newSize);

                bool oldVersion = oldChunk.ChunkHeader.Version != TFChunk.TFChunk.CurrentChunkVersion;
                long oldSize = oldChunk.FileSize;

                if (oldSize <= newSize && !alwaysKeepScavenged && !_unsafeIgnoreHardDeletes && !oldVersion)
                {
                    Log.Trace("Scavenging of chunk:");
                    Log.Trace(oldChunkName);
                    Log.Trace("completed in {0}.", sw.Elapsed);
                    Log.Trace("Old chunk' versions are kept as they are smaller.");
                    Log.Trace("Old chunk total size: {0}, scavenged chunk size: {1}.", oldSize, newSize);
                    Log.Trace("Scavenged chunk removed.");

                    newChunk.MarkForDeletion();
                    _scavengerLog.ChunksNotScavenged(chunkStartNumber, chunkEndNumber, sw.Elapsed, "");
                }
                else
                {
                    var positionMapping = new List<PosMap>(filteredCount);

                    TraverseChunkBasic(oldChunk, ct,
                        (result, i) =>
                        {                            
                            if (filteredIndexes[i])
                                positionMapping.Add(WriteRecord(newChunk, result.LogRecord));
                        });

                    newChunk.CompleteScavenge(positionMapping);

                    if (_unsafeIgnoreHardDeletes)
                    {
                        Log.Trace("Forcing scavenge chunk to be kept even if bigger.");
                    }

                    if (oldVersion)
                    {
                        Log.Trace("Forcing scavenged chunk to be kept as old chunk is a previous version.");
                    }

                    var chunk = _db.Manager.SwitchChunk(newChunk, verifyHash: false, removeChunksWithGreaterNumbers: false);
                    if (chunk != null)
                    {
                        Log.Trace("Scavenging of chunks:");
                        Log.Trace(oldChunkName);
                        Log.Trace("completed in {0}.", sw.Elapsed);
                        Log.Trace("New chunk: {0} --> #{1}-{2} ({3}).", Path.GetFileName(tmpChunkPath), chunkStartNumber,
                            chunkEndNumber, Path.GetFileName(chunk.FileName));
                        Log.Trace("Old chunk total size: {0}, scavenged chunk size: {1}.", oldSize, newSize);
                        var spaceSaved = oldSize - newSize;
                        _scavengerLog.ChunksScavenged(chunkStartNumber, chunkEndNumber, sw.Elapsed, spaceSaved);
                    }
                    else
                    {
                        Log.Trace("Scavenging of chunks: {0}", oldChunkName);
                        Log.Trace("completed in {1}.", sw.Elapsed);
                        Log.Trace("But switching was prevented for new chunk: #{0}-{1} ({2}).", chunkStartNumber, chunkEndNumber,
                            Path.GetFileName(tmpChunkPath));
                        Log.Trace("Old chunks total size: {0}, scavenged chunk size: {1}.", oldSize, newSize);
                        _scavengerLog.ChunksNotScavenged(chunkStartNumber, chunkEndNumber, sw.Elapsed, "Chunk switch prevented.");
                    }
                }
            }
            catch (FileBeingDeletedException exc)
            {
                Log.Info("Got FileBeingDeletedException exception during scavenging, that probably means some chunks were re-replicated.");
                Log.Info("Scavenging of following chunks will be skipped: {0}", oldChunkName);
                Log.Info("Stopping scavenging and removing temp chunk '{0}'...", tmpChunkPath);
                Log.Info("Exception message: {0}.", exc.Message);
                DeleteTempChunk(tmpChunkPath, MaxRetryCount);
                _scavengerLog.ChunksNotScavenged(chunkStartNumber, chunkEndNumber, sw.Elapsed, exc.Message);
            }
            catch (OperationCanceledException)
            {
                Log.Info("Scavenging cancelled at: {0}", oldChunkName);
                newChunk.MarkForDeletion();
                _scavengerLog.ChunksNotScavenged(chunkStartNumber, chunkEndNumber, sw.Elapsed, "Scavenge cancelled");
            }
            catch (Exception ex)
            {
                Log.Info("Got exception while scavenging chunk: #{0}-{1}. This chunk will be skipped\n"
                         + "Exception: {2}.", chunkStartNumber, chunkEndNumber, ex.ToString());
                DeleteTempChunk(tmpChunkPath, MaxRetryCount);
                _scavengerLog.ChunksNotScavenged(chunkStartNumber, chunkEndNumber, sw.Elapsed, ex.Message);
            }
        }

        private bool MergeChunks(IList<TFChunk.TFChunk> oldChunks, CancellationToken ct)
        {
            if (oldChunks.IsEmpty()) throw new ArgumentException("Provided list of chunks to merge is empty.");

            var oldChunksList = string.Join("\n", oldChunks);

            if (oldChunks.Count < 2)
            {
                Log.Trace("SCAVENGING: Tried to merge less than 2 chunks, aborting: {0}", oldChunksList);
                return false;
            }

            var sw = Stopwatch.StartNew();

            int chunkStartNumber = oldChunks.First().ChunkHeader.ChunkStartNumber;
            int chunkEndNumber = oldChunks.Last().ChunkHeader.ChunkEndNumber;

            var tmpChunkPath = Path.Combine(_db.Config.Path, Guid.NewGuid() + ".merge.scavenge.tmp");
            Log.Trace("SCAVENGING: started to merge chunks: {0}", oldChunksList);
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
                    initialReaderCount: _db.Config.InitialReaderCount,
                    reduceFileCachePressure: _db.Config.ReduceFileCachePressure);
            }
            catch (IOException exc)
            {
                Log.ErrorException(exc, "IOException during creating new chunk for scavenging merge purposes. Stopping scavenging merge process...");
                return false;
            }

            try
            {
                var oldVersion = oldChunks.Any(x => x.ChunkHeader.Version != TFChunk.TFChunk.CurrentChunkVersion);

                var positionMapping = new List<PosMap>();
                foreach (var oldChunk in oldChunks)
                {
                    TraverseChunkBasic(oldChunk, ct,
                        (result, i) =>
                        {
                            positionMapping.Add(WriteRecord(newChunk, result.LogRecord));
                        });
                }

                newChunk.CompleteScavenge(positionMapping);

                if (_unsafeIgnoreHardDeletes)
                {
                    Log.Trace("Forcing merged chunk to be kept even if bigger.");
                }

                if (oldVersion)
                {
                    Log.Trace("Forcing merged chunk to be kept as old chunk is a previous version.");
                }

                var chunk = _db.Manager.SwitchChunk(newChunk, verifyHash: false, removeChunksWithGreaterNumbers: false);
                if (chunk != null)
                {
                    Log.Trace("Merging of chunks:");
                    Log.Trace(oldChunksList);
                    Log.Trace("completed in {0}.", sw.Elapsed);
                    Log.Trace("New chunk: {0} --> #{1}-{2} ({3}).", Path.GetFileName(tmpChunkPath), chunkStartNumber,
                        chunkEndNumber, Path.GetFileName(chunk.FileName));
                    var spaceSaved = newChunk.FileSize - oldChunks.Sum(_ => _.FileSize);
                    _scavengerLog.ChunksMerged(chunkStartNumber, chunkEndNumber, sw.Elapsed, spaceSaved);
                    return true;
                }
                else
                {
                    Log.Trace("Merging of chunks:");
                    Log.Trace("{0}", oldChunksList);
                    Log.Trace("completed in {1}.", sw.Elapsed);
                    Log.Trace("But switching was prevented for new chunk: #{0}-{1} ({2}).", chunkStartNumber, chunkEndNumber, Path.GetFileName(tmpChunkPath));
                    _scavengerLog.ChunksNotMerged(chunkStartNumber, chunkEndNumber, sw.Elapsed, "Chunk switch prevented.");
                    return false;
                }
            }
            catch (FileBeingDeletedException exc)
            {
                Log.Info("Got FileBeingDeletedException exception during scavenge merging, that probably means some chunks were re-replicated.");
                Log.Info("Merging of following chunks will be skipped:");
                Log.Info("{0}", oldChunksList);
                Log.Info("Stopping merging and removing temp chunk '{0}'...", tmpChunkPath);
                Log.Info("Exception message: {0}.", exc.Message);
                DeleteTempChunk(tmpChunkPath, MaxRetryCount);
                _scavengerLog.ChunksNotMerged(chunkStartNumber, chunkEndNumber, sw.Elapsed, exc.Message);
                return false;
            }
            catch (OperationCanceledException)
            {
                Log.Info("Scavenging cancelled at:");
                Log.Info("{0}", oldChunksList);
                newChunk.MarkForDeletion();
                _scavengerLog.ChunksNotMerged(chunkStartNumber, chunkEndNumber, sw.Elapsed, "Scavenge cancelled");
                return false;
            }
            catch (Exception ex)
            {
                Log.Info("Got exception while merging chunk:");
                Log.Info("{0}", oldChunks);
                Log.Info("Exception: {0}.", ex.ToString());
                DeleteTempChunk(tmpChunkPath, MaxRetryCount);
                _scavengerLog.ChunksNotMerged(chunkStartNumber, chunkEndNumber, sw.Elapsed, ex.Message);
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
            catch (Exception ex)
            {
                if (retries > 0)
                {
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

        private bool ShouldKeepPrepare(PrepareLogRecord prepare, Dictionary<long, CommitInfo> commits, long chunkStart, long chunkEnd)
        {
            CommitInfo commitInfo;
            bool isCommitted = commits.TryGetValue(prepare.TransactionPosition, out commitInfo)
                               || prepare.Flags.HasAnyOf(PrepareFlags.IsCommitted);

            if (prepare.Flags.HasAnyOf(PrepareFlags.StreamDelete))
            {
                if (_unsafeIgnoreHardDeletes)
                {
                    Log.Info("Removing hard deleted stream tombstone for stream {0} at position {1}", prepare.EventStreamId, prepare.TransactionPosition);
                    commitInfo.TryNotToKeep();
                }
                else
                {
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

            if (!KeepOnlyFirstEventOfDuplicate(_tableIndex, prepare, eventNumber))
            {
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

        private bool KeepOnlyFirstEventOfDuplicate(ITableIndex tableIndex, PrepareLogRecord prepare, long eventNumber)
        {
            var result = _readIndex.ReadEvent(prepare.EventStreamId, eventNumber);
            if (result.Result == ReadEventResult.Success && result.Record.LogPosition != prepare.LogPosition) return false;
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

        private int TraverseChunkBasic(TFChunk.TFChunk chunk, CancellationToken ct,
            Action<RecordReadResult, int> process)
        {
            int i = 0;
            var result = chunk.TryReadFirst();
            while (result.Success)
            {
                process(result, i++);

                ct.ThrowIfCancellationRequested();

                result = chunk.TryReadClosestForward(result.NextPosition);
            }

            return i;
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