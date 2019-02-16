namespace EventStore.Projections.Core.Services.Processing {
	public class BypassingEventFilter : EventFilter {
		public BypassingEventFilter()
			: base(true, true, null) {
		}

		protected override bool DeletedNotificationPasses(string positionStreamId) {
			return true;
		}

		public override bool PassesSource(bool resolvedFromLinkTo, string positionStreamId, string eventType) {
			return true;
		}

		public override string GetCategory(string positionStreamId) {
			return null;
		}
	}
}
