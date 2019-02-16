namespace EventStore.Projections.Core.Standard {
	public class StreamCategoryExtractorByFirstSeparator : StreamCategoryExtractor {
		private readonly char _separator;

		public StreamCategoryExtractorByFirstSeparator(char separator) {
			_separator = separator;
		}

		public override string GetCategoryByStreamId(string streamId) {
			string category = null;
			if (!streamId.StartsWith("$")) {
				var lastSeparatorPosition = streamId.IndexOf(_separator);
				if (lastSeparatorPosition > 0)
					category = streamId.Substring(0, lastSeparatorPosition);
			}

			return category;
		}
	}
}
