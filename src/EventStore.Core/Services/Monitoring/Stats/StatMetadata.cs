// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

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
