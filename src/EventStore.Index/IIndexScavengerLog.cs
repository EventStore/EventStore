using System;

namespace EventStore.Core.Index {
	public interface IIndexScavengerLog {
		void IndexTableScavenged(int level, int index, TimeSpan elapsed, long entriesDeleted, long entriesKept,
			long spaceSaved);

		void IndexTableNotScavenged(int level, int index, TimeSpan elapsed, long entriesKept, string errorMessage);
	}
}
