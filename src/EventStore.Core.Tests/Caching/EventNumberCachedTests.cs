using System.Collections.Generic;
using EventStore.Core.Caching;
using NUnit.Framework;
using EventNumberCached = EventStore.Core.Services.Storage.ReaderIndex.IndexBackend<string>.EventNumberCached;

namespace EventStore.Core.Tests.Caching {
	public class EventNumberCachedTests {
		[Test]
		public void size_is_measured_correctly() {
			var mem = MemUsage<EventNumberCached[]>.Calculate(() =>
				new EventNumberCached[] { // we need an array to force an allocation
					new(10, 123)
				}, out _);

			Assert.AreEqual(mem, EventNumberCached.Size + MemSizer.ArraySize);
		}

		[Test]
		public void dictionary_entry_overhead_is_measured_correctly_with_string_key() {
			var mem3 = MemUsage<object>.Calculate(
				() => new Dictionary<string, EventNumberCached>(3),
				out _);

			var mem7 = MemUsage<object>.Calculate(
				() => new Dictionary<string, EventNumberCached>(7),
				out _);

			Assert.AreEqual(mem7 - mem3, (EventNumberCached.DictionaryEntryOverhead + EventNumberCached.Size) * 4);
		}

		[Test]
		public void dictionary_entry_overhead_is_measured_correctly_with_long_key() {
			var mem3 = MemUsage<object>.Calculate(
				() => new Dictionary<long, EventNumberCached>(3),
				out _);

			var mem7 = MemUsage<object>.Calculate(
				() => new Dictionary<long, EventNumberCached>(7),
				out _);

			Assert.AreEqual(mem7 - mem3, (EventNumberCached.DictionaryEntryOverhead + EventNumberCached.Size) * 4);
		}
	}
}
