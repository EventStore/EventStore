namespace EventStore.Projections.Core.Utils {
	public static class Logging {
		public static readonly string[] FilteredMessages =
			{"$get-state", "$state", "$get-result", "$result", "$statistics-report"};
	}
}
