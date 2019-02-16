using System.Collections.Generic;

namespace EventStore.Core.Tests.Infrastructure {
	public interface IRandTestItemProcessor {
		IEnumerable<RandTestQueueItem> ProcessedItems { get; }

		void Process(int iteration, RandTestQueueItem item);
		void LogMessages();
	}
}
