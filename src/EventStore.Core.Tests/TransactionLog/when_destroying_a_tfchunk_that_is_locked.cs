// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.IO;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.Chunks.TFChunk;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog;

[TestFixture]
public class when_destroying_a_tfchunk_that_is_locked : SpecificationWithFile {
	private TFChunk _chunk;
	private TFChunkBulkReader _reader;

	[SetUp]
	public override async Task SetUp() {
		await base.SetUp();
		_chunk = await TFChunkHelper.CreateNewChunk(Filename, 1000);
		await _chunk.Complete(CancellationToken.None);
		_chunk.UnCacheFromMemory();
		_reader = _chunk.AcquireRawReader();
		await _chunk.MarkForDeletion(CancellationToken.None);
	}

	[TearDown]
	public override async Task TearDown() {
		_reader.Release();
		await _chunk.MarkForDeletion(CancellationToken.None);
		_chunk.WaitForDestroy(2000);
		await base.TearDown();
	}

	[Test]
	public void the_file_is_not_deleted() {
		Assert.IsTrue(File.Exists(Filename));
	}
}
