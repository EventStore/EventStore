using System;
using System.Threading;
using EventStore.Core.Index;
using Serilog;

namespace EventStore.Core.TransactionLog.Scavenging {
	public class IndexExecutor {
		protected static readonly ILogger Log = Serilog.Log.ForContext<IndexExecutor>();
	}

	public class IndexExecutor<TStreamId> : IndexExecutor, IIndexExecutor<TStreamId> {
		private readonly IIndexScavenger _indexScavenger;
		private readonly IChunkReaderForIndexExecutor<TStreamId> _streamLookup;
		private readonly bool _unsafeIgnoreHardDeletes;
		private readonly int _restPeriod;
		private readonly Throttle _throttle;

		public IndexExecutor(
			IIndexScavenger indexScavenger,
			IChunkReaderForIndexExecutor<TStreamId> streamLookup,
			bool unsafeIgnoreHardDeletes,
			int restPeriod,
			Throttle throttle) {

			_indexScavenger = indexScavenger;
			_streamLookup = streamLookup;
			_unsafeIgnoreHardDeletes = unsafeIgnoreHardDeletes;
			_restPeriod = restPeriod;
			_throttle = throttle;
		}

		public void Execute(
			ScavengePoint scavengePoint,
			IScavengeStateForIndexExecutor<TStreamId> state,
			IIndexScavengerLog scavengerLogger,
			CancellationToken cancellationToken) {

			Log.Debug("SCAVENGING: Starting new scavenge index execution phase for {scavengePoint}",
				scavengePoint.GetName());

			var checkpoint = new ScavengeCheckpoint.ExecutingIndex(scavengePoint);
			state.SetCheckpoint(checkpoint);
			Execute(checkpoint, state, scavengerLogger, cancellationToken);
		}

		public void Execute(
			ScavengeCheckpoint.ExecutingIndex checkpoint,
			IScavengeStateForIndexExecutor<TStreamId> state,
			IIndexScavengerLog scavengerLogger,
			CancellationToken cancellationToken) {

			Log.Debug("SCAVENGING: Executing indexes from checkpoint: {checkpoint}", checkpoint);

			_indexScavenger.ScavengeIndex(
				scavengePoint: checkpoint.ScavengePoint.Position,
				shouldKeep: GenShouldKeep(
					checkpoint.ScavengePoint,
					state,
					cancellationToken),
				log: scavengerLogger,
				cancellationToken: cancellationToken);
		}

		private Func<IndexEntry, bool> GenShouldKeep(
			ScavengePoint scavengePoint,
			IScavengeStateForIndexExecutor<TStreamId> state,
			CancellationToken cancellationToken) {

			// we cache some stream info between invocations of ShouldKeep out here since it will
			// typically be invoked repeatedly for the same stream.
			var currentHash = (ulong?)null;
			var currentHashIsCollision = false;
			var currentPosition = long.MaxValue;
			var currentDiscardPoint = DiscardPoint.KeepAll;
			var currentIsTombstoned = false;
			var currentIsDefinitelyMetastream = false;

			var restCounter = 0;
			var scavengePointPosition = scavengePoint.Position;

			bool ShouldKeep(IndexEntry indexEntry) {
				// Rest occasionally
				if (++restCounter == _restPeriod) {
					restCounter = 0;
					_throttle.Rest(cancellationToken);
				}

				if (indexEntry.Position >= scavengePointPosition) {
					// discard point will respect this anyway, but this is faster.
					return true;
				}

				if (currentHash != indexEntry.Stream || currentHashIsCollision) {
					// either the hash changed or (definitely on to a different stream) or
					// the currentHash is a collision (maybe on to a different stream).
					// need to set all 5 of the current* variables correctly.

					currentHash = indexEntry.Stream;
					currentHashIsCollision = state.IsCollision(indexEntry.Stream);
					currentPosition = indexEntry.Position;

					StreamHandle<TStreamId> handle = default;

					if (currentHashIsCollision) {
						// hash isn't enough to identify the stream. get its id.
						if (!_streamLookup.TryGetStreamId(indexEntry.Position, out var streamId)) {
							// there is no record at this position to get the stream from.
							// we should definitely discard the entry (just like old index scavenge does)
							// we can't even tell which stream it is for.
							return false;
						} else {
							// we got a streamId, which means we must have found a record at this
							// position, but that doesn't necessarily mean we want to keep the IndexEntry
							// the log record might still exist only because its chunk hasn't reached
							// the threshold.
							handle = StreamHandle.ForStreamId(streamId);
						}
					} else {
						// not a collision, we can get the discard point by hash.
						handle = StreamHandle.ForHash<TStreamId>(currentHash.Value);
					}

					if (state.TryGetIndexExecutionInfo(handle, out var info)) {
						currentIsTombstoned = info.IsTombstoned;
						currentDiscardPoint = info.DiscardPoint;
						currentIsDefinitelyMetastream = info.IsMetastream;
					} else {
						// this stream has no scavenge data accumulated. therefore is has no metadata
						// and is not tombstoned.
						currentIsTombstoned = false;
						currentDiscardPoint = DiscardPoint.KeepAll;
						currentIsDefinitelyMetastream = false;
						return true; // don't need this but may as well.
					}
				} else {
					// same hash as the previous invocation, and it is not a collision, so it must be for
					// the same stream, so the current* variables are already correct.

					if (indexEntry.Position >= currentPosition) {
						// ptables are arranged (hash, version, position) descending. so for a given hash
						// we will iterate through the versions descending. previous bugs have allowed
						// events to be written occasionally with the wrong version number. we spot this
						// here and log about it.
						var stream = default(TStreamId);
						try {
							stream = state.LookupUniqueHashUser(indexEntry.Stream);
						} catch {
							// probably this isn't possible
						}

						Log.Debug(
							"SCAVENGING: Found out of order index entry. " +
							"Stream \"{stream}\" has index entry {indexEntry} but " +
							"previously saw index entry with position {previousPosition}.",
							stream, indexEntry,
							currentPosition);
					}
					currentPosition = indexEntry.Position;
				}

				// all the current* variables are now set correctly.
				if (currentIsTombstoned) {
					if (_unsafeIgnoreHardDeletes) {
						// remove _everything_ for metadata and original streams
						return false;
					}

					if (currentIsDefinitelyMetastream) {
						// when the original stream is tombstoned we can discard the _whole_ metastream
						return false;
					}

					// otherwise obey the discard points below.
				}

				var shouldDiscard = currentDiscardPoint.ShouldDiscard(indexEntry.Version);
				return !shouldDiscard;
			}

			return ShouldKeep;
		}
	}
}
