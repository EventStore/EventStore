using EventStore.Common.Utils;

namespace EventStore.Core.Caching {
	public abstract class AllotmentResizer {
		public string Name => Allotment.Name;
		public long Size => Allotment.Size;
		protected IAllotment Allotment { get; }
		public string Unit { get; }

		protected AllotmentResizer(
			string unit,
			IAllotment allotment) {
			Ensure.NotNull(unit, nameof(unit));
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
