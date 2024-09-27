using EventStore.Core.Exceptions;
using EventStore.Core.Index;
using NUnit.Framework;

namespace EventStore.Core.Tests.Index.IndexV1;

public class when_constructing_v1_ptable : SpecificationWithDirectoryPerTestFixture {
	[Test]
	public void an_exception_is_thrown() {
		Assert.Throws<CorruptIndexException>(() => {
			using var table = PTable.FromMemtable(
				new HashListMemTable(PTableVersions.IndexV1, maxSize: 20),
				GetTempFilePath(),
				Constants.PTableInitialReaderCount,
				Constants.PTableMaxReaderCountDefault);
		});
	}
}
