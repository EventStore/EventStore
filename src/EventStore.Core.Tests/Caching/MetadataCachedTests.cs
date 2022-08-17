using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using EventStore.Core.Caching;
using EventStore.Core.Data;
using NUnit.Framework;
using MetadataCached = EventStore.Core.Services.Storage.ReaderIndex.IndexBackend<string>.MetadataCached;

namespace EventStore.Core.Tests.Caching {
	public class MetadataCachedTests {
		[Test]
		public void size_is_measured_correctly() {
			var mem = MemUsage<MetadataCached[]>.Calculate(() =>
				new MetadataCached[] { // we need an array to force an allocation
					new(0, new StreamMetadata(
						maxCount: 10,
						maxAge: new TimeSpan(10),
						truncateBefore: null,
						tempStream: false,
						cacheControl: null,
						acl: new StreamAcl(
							new[] { new string(' ', 1), new string(' ', 2) },
							new[] { new string(' ', 0) },
							null,
							null,
							new[] { new string(' ', 1), new string(' ', 3), new string(' ', 3), new string(' ', 7) }
						)
					))
				}, out var metadata);

			Assert.AreEqual(mem, metadata[0].Size + MemSizer.ArraySize);
		}

		[Test]
		public void dictionary_entry_overhead_is_measured_correctly_with_string_key() {
			var mem3 = MemUsage<object>.Calculate(
				() => new Dictionary<string, MetadataCached>(3),
				out _);

			var mem7 = MemUsage<object>.Calculate(
				() => new Dictionary<string, MetadataCached>(7),
				out _);

			Assert.AreEqual(mem7 - mem3, (MetadataCached.DictionaryEntryOverhead + Unsafe.SizeOf<MetadataCached>()) * 4);
		}

		[Test]
		public void dictionary_entry_overhead_is_measured_correctly_with_long_key() {
			var mem3 = MemUsage<object>.Calculate(
				() => new Dictionary<long, MetadataCached>(3),
				out _);

			var mem7 = MemUsage<object>.Calculate(
				() => new Dictionary<long, MetadataCached>(7),
				out _);

			Assert.AreEqual(mem7 - mem3, (MetadataCached.DictionaryEntryOverhead + Unsafe.SizeOf<MetadataCached>()) * 4);
		}
	}
}
