using System;
using EventStore.Core.Data;

namespace EventStore.Core.TransactionLog.Scavenging {
	// This calculates information about the event so that the main calculator can decide what to do
	public class EventCalculator<TStreamId> {
		public EventCalculator(
			int chunkSize,
			IScavengeStateForCalculatorReadOnly<TStreamId> state,
			ScavengePoint scavengePoint,
			StreamCalculator<TStreamId> streamCalc) {

			ChunkSize = chunkSize;
			State = state;
			ScavengePoint = scavengePoint;
			Stream = streamCalc;
		}

		public void SetEvent(EventInfo eventInfo) {
			EventInfo = eventInfo;
		}

		// State that doesn't change. scoped to the scavenge.
		public int ChunkSize { get; }
		public IScavengeStateForCalculatorReadOnly<TStreamId> State { get; }
		public ScavengePoint ScavengePoint { get; }
		public StreamCalculator<TStreamId> Stream { get; }

		// State that is scoped to the event.
		public EventInfo EventInfo { get; private set; }

		public bool IsOnOrAfterScavengePoint => EventInfo.LogPosition >= ScavengePoint.Position;

		public int LogicalChunkNumber => (int)(EventInfo.LogPosition / ChunkSize);

		public DiscardDecision DecideEvent() {
			// Events in original streams can be discarded because of:
			//   Tombstones, TruncateBefore, MaxCount, MaxAge.
			//
			// any one of these is enough to warrant a discard
			// however none of them allow us to scavenge the last event
			// or anything beyond the scavenge point, so we limit by that.

			// respect the scavenge point
			if (IsOnOrAfterScavengePoint) {
				return DiscardDecision.Keep;
			}

			// keep last event instream
			// to be extra safe, we keep if it is after the 'last event' too, which should never happen.
			if (EventInfo.EventNumber >= Stream.LastEventNumber) {
				return DiscardDecision.Keep;
			}

			// for tombstoned streams, discard everything that isn't the last event
			if (Stream.IsTombstoned) {
				// we already know this is not the last event, so discard it.
				return DiscardDecision.Discard;
			}

			// truncatebefore, maxcount
			if (Stream.TruncateBeforeOrMaxCountDiscardPoint.ShouldDiscard(EventInfo.EventNumber)) {
				return DiscardDecision.Discard;
			}

			// up to maxage. keep if there is no maxage restriction
			var cutoffTime = Stream.CutoffTime;
			if (!cutoffTime.HasValue) {
				return DiscardDecision.Keep;
			}

			// there is a maxage restriction
			return ShouldDiscardForMaxAge(cutoffTime.Value);
		}

		private DiscardDecision ShouldDiscardForMaxAge(DateTime cutoffTime) {
			// establish a the range that the event was definitely created between.
			if (!State.TryGetChunkTimeStampRange(LogicalChunkNumber, out var createdAtRange)) {
				throw new Exception($"Could not get TimeStamp range for chunk {LogicalChunkNumber}");
			}

			// range is guanranteed to be non-empty
			if (cutoffTime < createdAtRange.Min) {
				return DiscardDecision.Keep;
			}

			if (createdAtRange.Max < cutoffTime) {
				return DiscardDecision.Discard;
			}

			// it must be between min and max inclusive
			return DiscardDecision.MaybeDiscard;
		}
	}
}
