using System;
using EventStore.Core.Caching;

namespace EventStore.Core.Tests.Caching {
	public class AdHocAllotment : IAllotment {
		private readonly Func<long> _getSize;
		private readonly Action<long> _setCapacity;

		public AdHocAllotment(Func<long> getSize, Action<long> setCapacity) {
			_getSize = getSize;
			_setCapacity = setCapacity;
		}

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
