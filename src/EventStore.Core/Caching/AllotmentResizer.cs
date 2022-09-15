using EventStore.Common.Utils;

namespace EventStore.Core.Caching {
	public abstract class AllotmentResizer {
		public string Name => Allotment.Name;
		public long Size => Allotment.Size;
		protected IAllotment Allotment { get; }
		public ResizerUnit Unit { get; }

		protected AllotmentResizer(
			ResizerUnit unit,
			IAllotment allotment) {
			Ensure.NotNull(allotment, nameof(allotment));

			Allotment = allotment;
			Unit = unit;
		}

		protected string BuildStatsKey(string parentKey) =>
			parentKey.Length == 0 ? Name : $"{parentKey}-{Name}";

		protected static string GetParentKey(string key) {
			var index = key.LastIndexOf('-');
			return index < 0 ? null : key[..index];
		}
	}
}
