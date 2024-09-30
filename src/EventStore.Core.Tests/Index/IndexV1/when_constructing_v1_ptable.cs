// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

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
