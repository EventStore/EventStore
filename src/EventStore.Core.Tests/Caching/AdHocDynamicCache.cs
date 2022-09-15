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

		private long _capacity;
		public long Capacity {
			get => _capacity;
			set {
				_capacity = value;
				_setCapacity(value);
			}
		}

		public long Size => _getSize();
	}
}
