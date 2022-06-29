using System;
using System.Diagnostics;
using System.Threading;
using EventStore.Common.Log;

namespace EventStore.Core.TransactionLog.Scavenging {
	public class Calculator {
		protected static ILogger Log { get; } = LogManager.GetLoggerFor<Accumulator>();
	}

	public class Calculator<TStreamId> : Calculator, ICalculator<TStreamId> {
		private readonly IIndexReaderForCalculator<TStreamId> _index;
		private readonly int _chunkSize;
		private readonly int _cancellationCheckPeriod;
		private readonly int _checkpointPeriod;
		private readonly Throttle _throttle;

		public Calculator(
			IIndexReaderForCalculator<TStreamId> index,
			int chunkSize,
			int cancellationCheckPeriod,
			int checkpointPeriod,
			Throttle throttle) {

			_index = index;
			_chunkSize = chunkSize;
			_cancellationCheckPeriod = cancellationCheckPeriod;
			_checkpointPeriod = checkpointPeriod;
			_throttle = throttle;
		}

		public void Calculate(
			ScavengePoint scavengePoint,
			IScavengeStateForCalculator<TStreamId> state,
			CancellationToken cancellationToken) {

			Log.Trace("SCAVENGING: Starting new scavenge calculation phase for {scavengePoint}",
				scavengePoint.GetName());

			var checkpoint = new ScavengeCheckpoint.Calculating<TStreamId>(
				scavengePoint: scavengePoint,
				doneStreamHandle: default);
			state.SetCheckpoint(checkpoint);
			Calculate(checkpoint, state, cancellationToken);
		}

		public void Calculate(
			ScavengeCheckpoint.Calculating<TStreamId> checkpoint,
			IScavengeStateForCalculator<TStreamId> state,
			CancellationToken cancellationToken) {

			Log.Trace("SCAVENGING: Calculating from checkpoint: {checkpoint}", checkpoint);
			var stopwatch = Stopwatch.StartNew();

			var weights = new WeightAccumulator(state);
			var scavengePoint = checkpoint.ScavengePoint;
			var streamCalc = new StreamCalculator<TStreamId>(_index, scavengePoint);
			var eventCalc = new EventCalculator<TStreamId>(_chunkSize, state, scavengePoint, streamCalc);

			var checkpointCounter = 0;
			var cancellationCheckCounter = 0;
			var periodStart = stopwatch.Elapsed;
			var totalCounter = 0;

			// iterate through the original (i.e. non-meta) streams that need scavenging (i.e.
			// those that have metadata or tombstones)
			// - for each one use the accumulated data to set/update the discard points of the stream.
			// - along the way add weight to the affected chunks.
			// note that the discard points will never discard the last event from the stream.
			// there are limited scenarios when we do want to do this, (metadata streams when the main
			// stream is tombstoned, and when UnsafeIgnoreHardDeletes is true) and they are governed by
			// the IsTombstoned flags.
			var originalStreamsToCalculate = state.OriginalStreamsToCalculate(
				checkpoint: checkpoint?.DoneStreamHandle ?? default);

			// for determinism it is important that IncreaseChunkWeight is called in a transaction with
			// its calculation and checkpoint, otherwise the weight could be increased again on recovery
			var transaction = state.BeginTransaction();
			try {
				foreach (var (originalStreamHandle, originalStreamData) in originalStreamsToCalculate) {
					if (originalStreamData.Status != CalculationStatus.Active)
						throw new InvalidOperationException(
							$"Attempted to calculate a {originalStreamData.Status} record: {originalStreamData}");

					// todo: if sqlite lets us update the 'current' row before continuing on to the next
					// then this could take advantage of that.

					streamCalc.SetStream(originalStreamHandle, originalStreamData);
					var newStatus = streamCalc.CalculateStatus();

					CalculateDiscardPointsForOriginalStream(
						eventCalc,
						weights,
						originalStreamHandle,
						scavengePoint,
						cancellationToken,
						ref cancellationCheckCounter,
						out var newDiscardPoint,
						out var newMaybeDiscardPoint);

					// don't allow the discard point to move backwards
					if (newDiscardPoint < originalStreamData.DiscardPoint) {
						newDiscardPoint = originalStreamData.DiscardPoint;
					}

					// don't allow the maybe discard point to move backwards
					if (newMaybeDiscardPoint < originalStreamData.MaybeDiscardPoint) {
						newMaybeDiscardPoint = originalStreamData.MaybeDiscardPoint;
					}

					if (newStatus == originalStreamData.Status &&
						newDiscardPoint == originalStreamData.DiscardPoint &&
						newMaybeDiscardPoint == originalStreamData.MaybeDiscardPoint) {

						// nothing to update for this stream
					} else {
						state.SetOriginalStreamDiscardPoints(
							streamHandle: originalStreamHandle,
							status: newStatus,
							discardPoint: newDiscardPoint,
							maybeDiscardPoint: newMaybeDiscardPoint);
					}

					totalCounter++;

					// Checkpoint occasionally
					if (++checkpointCounter == _checkpointPeriod) {
						checkpointCounter = 0;
						weights.Flush();
						transaction.Commit(new ScavengeCheckpoint.Calculating<TStreamId>(
							scavengePoint,
							originalStreamHandle));
						transaction = state.BeginTransaction();

						var elapsed = stopwatch.Elapsed;
						LogRate("period", _checkpointPeriod, elapsed - periodStart);
						LogRate("total", totalCounter, elapsed);

						periodStart = stopwatch.Elapsed;
					}
				}

				// if we processed some streams, the last one is in the calculator
				// if we didn't process any streams, the calculator contains the default
				// none handle, which is probably appropriate to commit in that case
				weights.Flush();
				transaction.Commit(new ScavengeCheckpoint.Calculating<TStreamId>(
					scavengePoint,
					streamCalc.OriginalStreamHandle));
				LogRate("grand total (including rests)", totalCounter, stopwatch.Elapsed);
			} catch {
				// invariant: there is always an open transaction whenever an exception can be thrown
				transaction.Rollback();
				throw;
			}
			_throttle.Rest(cancellationToken);
		}

		private void LogRate(string name, int count, TimeSpan elapsed) {
			var rate = count / elapsed.TotalSeconds;
			Log.Trace(
				"SCAVENGING: Calculated in " + name + ": {count:N0} streams in {elapsed}. {rate:N2} streams per second",
				count, elapsed, rate);
		}

		// This does two things.
		// 1. Calculates and returns the discard points for this stream
		// 2. Adds weight to the affected chunks so that they get scavenged.
		//
		// The calculator determines that we can definitely discard everything up to the discardPoint
		// and we may be able to discard things between the discardPoint and the maybeDiscardPoint.
		//
		// We want to calculate the discard points from scratch, without considering what values they
		// came out as last time.
		private void CalculateDiscardPointsForOriginalStream(
			EventCalculator<TStreamId> eventCalc,
			WeightAccumulator weights,
			StreamHandle<TStreamId> originalStreamHandle,
			ScavengePoint scavengePoint,
			CancellationToken cancellationToken,
			ref int cancellationCheckCounter,
			out DiscardPoint discardPoint,
			out DiscardPoint maybeDiscardPoint) {

			var fromEventNumber = 0L;

			discardPoint = DiscardPoint.KeepAll;
			maybeDiscardPoint = DiscardPoint.KeepAll;

			//qq tuning: what would be sensible? probably pretty large
			const int maxCount = 100;

			var first = true;

			while (true) {
				// read in slices because the stream might be huge.
				// note: when the handle is a hash the ReadEventInfoForward call is index-only
				// note: the event infos are not necessarily contiguous
				var result = _index.ReadEventInfoForward(
					originalStreamHandle,
					fromEventNumber,
					maxCount,
					scavengePoint);

				var slice = result.EventInfos;

				foreach (var eventInfo in slice) {
					// Check cancellation and rest occasionally
					if (++cancellationCheckCounter == _cancellationCheckPeriod) {
						cancellationCheckCounter = 0;
						cancellationToken.ThrowIfCancellationRequested();
						_throttle.Rest(cancellationToken);
					}

					eventCalc.SetEvent(eventInfo);

					if (first) {
						// this is the first event that is known to the index. advance the discard points
						// to discard everything before here since they're already discarded. (we need
						// this because the chunks haven't necessarily been executed yet so we want to
						// make sure those records are removed when the chunks are scavenged. note that
						// chunk weight has already been added for them, so no need to do that again.
						discardPoint = DiscardPoint.DiscardBefore(eventInfo.EventNumber);
						maybeDiscardPoint = discardPoint;
						first = false;
					}

					switch (eventCalc.DecideEvent()) {
						case DiscardDecision.Discard:
							weights.OnDiscard(eventCalc.LogicalChunkNumber);
							discardPoint = DiscardPoint.DiscardIncluding(eventInfo.EventNumber);
							break;

						case DiscardDecision.MaybeDiscard:
							// add weight to the chunk so that this will be inspected more closely
							// it is possible that we already added weight on a previous scavenge and are
							// doing so again, but we must because the weight may have been reset
							// by the previous scavenge
							weights.OnMaybeDiscard(eventCalc.LogicalChunkNumber);
							maybeDiscardPoint = DiscardPoint.DiscardIncluding(eventInfo.EventNumber);
							break;

						case DiscardDecision.Keep:
							// found the first one to keep. we are done discarding. to help keep things
							// simple, move the maybe up to the discardpoint if it is behind.
							maybeDiscardPoint = maybeDiscardPoint.Or(discardPoint);
							return;

						default:
							throw new Exception(
								$"Invalid discard decision for stream {originalStreamHandle}.");
					}
				}

				// we haven't found an event to definitely keep
				if (result.IsEndOfStream) {
					// we have finished reading the stream from the index,
					// but not found any events to keep.
					// we therefore didn't find any at all, or found some and discarded them all
					// (the latter should not be possible)
					if (first) {
						// we didn't find any at all
						// - the stream might actually be empty
						// - the stream might have events after the scavenge point and old scavenge
						//   has removed the ones before
						// we didn't find anything to discard, so keep everything.
						discardPoint = DiscardPoint.KeepAll;
						maybeDiscardPoint = DiscardPoint.KeepAll;
						return;
					} else {
						// we found some and discarded them all, oops.
						throw new Exception(
							$"Discarded all events for stream {originalStreamHandle}. " +
							$"This should be impossible.");
					}
				} else {
					// we aren't done reading slices, read the next slice.
					fromEventNumber = result.NextEventNumber;
				}
			}
		}
	}
}
