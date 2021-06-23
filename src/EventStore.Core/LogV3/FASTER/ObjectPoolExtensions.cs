using EventStore.Core.DataStructures;

namespace EventStore.Core.LogV3.FASTER {
	public static class ObjectPoolExtensions {
		public static Lease<T> Rent<T>(this ObjectPool<T> pool) => new(pool);
	}
}

