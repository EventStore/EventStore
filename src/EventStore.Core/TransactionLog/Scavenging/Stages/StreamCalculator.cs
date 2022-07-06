using System;
using EventStore.Core.Data;

namespace EventStore.Core.TransactionLog.Scavenging {
	// the idea of this is we load some context into this class for the given stream
	// and then it helps us calculate and make decisions about what to keep.
	// and the calculations can be done on demand.
	// and can be unit tested separately if necessary
	// reused between streams to avoid allocations.
	public class StreamCalculator<TStreamId> {
		public StreamCalculator(
			IIndexReaderForCalculator<TStreamId> index,
			ScavengePoint scavengePoint) {

			Index = index;
			ScavengePoint = scavengePoint;
		}

		public void SetStream(
			StreamHandle<TStreamId> originalStreamHandle,
			OriginalStreamData originalStreamData) {

			_lastEventNumber = null;
			_truncateBeforeOrMaxCountDiscardPoint = null;

			OriginalStreamHandle = originalStreamHandle;
			OriginalStreamData = originalStreamData;
		}

		// State that doesn't change. scoped to the scavenge.
		public IIndexReaderForCalculator<TStreamId> Index { get; }
		public ScavengePoint ScavengePoint { get; }

		// State that is scoped to the stream
		public StreamHandle<TStreamId> OriginalStreamHandle { get; private set; }
		private OriginalStreamData OriginalStreamData { get; set; }

		// Returns NoStream (-1) if there are no events before the scavenge point.
		// Caller must handle that
		private long? _lastEventNumber;
		public long LastEventNumber {
			get {
				if (!_lastEventNumber.HasValue) {
					_lastEventNumber = Index.GetLastEventNumber(OriginalStreamHandle, ScavengePoint);
				}

				return _lastEventNumber.Value;
			}
		}

		public DiscardPoint TruncateBeforeDiscardPoint =>
			OriginalStreamData.TruncateBefore.HasValue
				? DiscardPoint.DiscardBefore(OriginalStreamData.TruncateBefore.Value)
				: DiscardPoint.KeepAll;

		public DiscardPoint MaxCountDiscardPoint =>
			// if LastEventNumber is NoStream (-1) this will always KeepAll as intended
			OriginalStreamData.MaxCount.HasValue
				? DiscardPoint.DiscardIncluding(LastEventNumber - OriginalStreamData.MaxCount.Value)
				: DiscardPoint.KeepAll;

		private DiscardPoint? _truncateBeforeOrMaxCountDiscardPoint;
		public DiscardPoint TruncateBeforeOrMaxCountDiscardPoint {
			get {
				if (!_truncateBeforeOrMaxCountDiscardPoint.HasValue) {
					_truncateBeforeOrMaxCountDiscardPoint =
						TruncateBeforeDiscardPoint.Or(MaxCountDiscardPoint);
				}

				return _truncateBeforeOrMaxCountDiscardPoint.Value;
			}
		}

		public bool IsTombstoned => OriginalStreamData.IsTombstoned;

		// We can discard the event when it is older than the cutoff
		public DateTime? CutoffTime => ScavengePoint.EffectiveNow - OriginalStreamData.MaxAge;

		// Calculates whether this stream needs recalculating, assuming the metadata and istombstoned
		// do not change (either of these updates will cause the calculator to reactivate it).
		public CalculationStatus CalculateStatus() {
			if (OriginalStreamData.IsTombstoned) {
				// discard points will not move after this, BUT it cannot be deleted because we might
				// run a scavenge with UnsafeHardDeletes in which case we will need to know this is
				// tombstoned in order to discard the tombstone from the index.
				return CalculationStatus.Archived;
			}

			if (OriginalStreamData.MaxAge.HasValue) {
				//  because time will have passed so discard points might need moving
				return CalculationStatus.Active;
			}

			if (OriginalStreamData.MaxCount.HasValue) {
				// new events might have been added so discard point might need moving
				// (unless the accumulator tracked when new events have been written per stream, but
				// this would likely not be worth it.)
				return CalculationStatus.Active;
			}

			var tb = OriginalStreamData.TruncateBefore;
			if (tb.HasValue &&
				tb != EventNumber.DeletedStream &&
				LastEventNumber < tb) {

				// unspent TB. new events would cause the discard point to move.
				// EventNumber.DeletedStream counts as spent because we would only need to
				// recalculate if a new event is written but in that case the database will
				// create for us a new metadata record, too.
				// this also works if LastEventNumber is NoStream (-1)
				return CalculationStatus.Active;
			}

			// Here it is not tombstoned, and the metadata is either a spent TB
			// or empty (which could happen if there was metadata that was
			// subsequently cleared).
			// Discard points will no longer move, we can delete it.
			return CalculationStatus.Spent;
		}
	}
}
