using System.Collections.Generic;
using EventStore.Core.Services;
using EventStore.Core.TransactionLog.Services;

namespace EventStore.Projections.Core.Services.Processing {
	public class TransactionFileEventFilter : EventFilter {
		private readonly bool _includeLinks;

		public TransactionFileEventFilter(
			bool allEvents, bool includeDeletedStreamEvents, HashSet<string> events, bool includeLinks = false)
			: base(allEvents, includeDeletedStreamEvents, events) {
			_includeLinks = includeLinks;
		}

		protected override bool DeletedNotificationPasses(string positionStreamId) {
			return true;
		}

		public override bool PassesSource(bool resolvedFromLinkTo, string positionStreamId, string eventType) {
			if (!_includeLinks && eventType == SystemEventTypes.LinkTo) return false;
			return (_includeLinks || !resolvedFromLinkTo)
			       && (!SystemStreams.IsSystemStream(positionStreamId)
			           || SystemStreams.IsMetastream(positionStreamId)
			           && !SystemStreams.IsSystemStream(SystemStreams.OriginalStreamOf(positionStreamId)));
		}

		public override string GetCategory(string positionStreamId) {
			return null;
		}
	}
}
