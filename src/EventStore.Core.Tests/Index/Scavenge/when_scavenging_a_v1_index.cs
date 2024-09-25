using System;
using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.Scavenge {
	public class when_scavenging_a_v1_index : SpecificationWithDirectoryPerTestFixture {
		[Test]
		public void is_no_longer_supported() {
			using var table = PTable.FromMemtable(
				new HashListMemTable(PTableVersions.IndexV1, maxSize: 20),
				GetTempFilePath(),
				Constants.PTableInitialReaderCount,
				Constants.PTableMaxReaderCountDefault);

			Assert.Throws<InvalidOperationException>(() => {
				using var scavenged = PTable.Scavenged(
					table,
					GetTempFilePath(),
					PTableVersions.IndexV4,
					_ => true,
					out var spaceSaved,
					skipIndexVerify: false,
					initialReaders: Constants.PTableInitialReaderCount,
					maxReaders: Constants.PTableMaxReaderCountDefault,
					useBloomFilter: true);
			});
		}
	}
}
