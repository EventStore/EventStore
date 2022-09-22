using System;
using EventStore.Core.Caching;

namespace EventStore.Core.Tests.Caching {
	public class AdHocDynamicCache : IDynamicCache {
		private readonly Func<long> _getSize;
		private readonly Action<long> _setCapacity;

		public AdHocDynamicCache(
			Func<long> getSize,
			Action<long> setCapacity,
			string name = null) {

			_getSize = getSize;
			_setCapacity = setCapacity;
			Name = name ?? nameof(AdHocDynamicCache);
		}

		public string Name { get; }

		public long Capacity { get; private set; }

		public long FreedSize { get; set; }

		public void SetCapacity(long value) {
			Capacity = value;
			_setCapacity(value);
		}

		public void ResetFreedSize() {
			FreedSize = 0;
		}

		public long Size => _getSize();
	}
}
