using System;
using EventStore.Core.Index;
using NUnit.Framework;
using System.Threading.Tasks;

namespace EventStore.Core.Tests.Index.IndexV4;

public class when_merging_ptables_v1_to_v4 : SpecificationWithDirectoryPerTestFixture {
	[Test]
	public void is_no_longer_supported() {
		using var table1 = PTable.FromMemtable(
			new HashListMemTable(PTableVersions.IndexV1, maxSize: 20),
			GetTempFilePath(),
			Constants.PTableInitialReaderCount,
			Constants.PTableMaxReaderCountDefault);

		using var table2 = PTable.FromMemtable(
			new HashListMemTable(PTableVersions.IndexV1, maxSize: 20),
			GetTempFilePath(),
			Constants.PTableInitialReaderCount,
			Constants.PTableMaxReaderCountDefault);

		Assert.Throws<InvalidOperationException>(() => {
			using var newtable = PTable.MergeTo(
				[table1, table2],
				GetTempFilePath(),
				PTableVersions.IndexV4,
				Constants.PTableInitialReaderCount,
				Constants.PTableMaxReaderCountDefault);
		});
	}
}
