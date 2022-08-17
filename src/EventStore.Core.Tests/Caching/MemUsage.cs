using System;

namespace EventStore.Core.Tests.Caching {
	public class MemUsage<T> {
		public static long Calculate(Func<T> createObject, out T newObject) {
			var memBefore = GC.GetAllocatedBytesForCurrentThread();
			newObject = createObject();
			var memAfter = GC.GetAllocatedBytesForCurrentThread();

			return memAfter - memBefore;
		}
	}
}
