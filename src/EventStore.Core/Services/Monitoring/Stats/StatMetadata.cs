namespace EventStore.Core.Services.Monitoring.Stats {
	public class StatMetadata {
		public object Value { get; set; }
		public string Category { get; set; }
		public string Title { get; set; }
		public bool DrawChart { get; set; }

		public StatMetadata() {
		}

		private StatMetadata(object value, string category, string title, bool drawChart) {
			Value = value;
			Category = category;
			Title = title;
			DrawChart = drawChart;
		}

		public StatMetadata(object value, string category, string title)
			: this(value, category, title, true) {
		}

		public StatMetadata(object value, string title)
			: this(value, null, title, true) {
		}
	}
}
