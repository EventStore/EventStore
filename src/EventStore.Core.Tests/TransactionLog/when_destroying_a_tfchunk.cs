// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.TransactionLog.Chunks.TFChunk;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog;

[TestFixture]
public class when_destroying_a_tfchunk : SpecificationWithFile {
	private TFChunk _chunk;

	[SetUp]
	public override async Task SetUp() {
		await base.SetUp();
		_chunk = await TFChunkHelper.CreateNewChunk(Filename, 1000);
		await _chunk.MarkForDeletion(CancellationToken.None);
	}

	[Test]
	public void the_file_is_deleted() {
		Assert.IsFalse(File.Exists(Filename));
	}
}
