// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

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
