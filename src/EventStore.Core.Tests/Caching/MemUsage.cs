using System;

namespace EventStore.Core.Tests.Caching {
	public class MemUsage {
		public static long Calculate<T>(Func<T> createObject, out T newObject) {
			var memBefore = GC.GetAllocatedBytesForCurrentThread();
			newObject = createObject();
			var memAfter = GC.GetAllocatedBytesForCurrentThread();

			return memAfter - memBefore;
		}

		public static long Calculate(Action createObject) {
			return Calculate<object>(() => {
				createObject();
				return null;
			}, out _);
		}
	}
}
