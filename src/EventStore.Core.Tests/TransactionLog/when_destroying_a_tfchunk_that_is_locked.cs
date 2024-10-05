// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.IO;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.Chunks.TFChunk;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog;

[TestFixture]
public class when_destroying_a_tfchunk_that_is_locked : SpecificationWithFile {
	private TFChunk _chunk;
	private TFChunkBulkReader _reader;

	[SetUp]
	public override void SetUp() {
		base.SetUp();
		_chunk = TFChunkHelper.CreateNewChunk(Filename, 1000);
		_chunk.Complete();
		_chunk.UnCacheFromMemory();
		_reader = _chunk.AcquireRawReader();
		_chunk.MarkForDeletion();
	}

	[TearDown]
	public override void TearDown() {
		_reader.Release();
		_chunk.MarkForDeletion();
		_chunk.WaitForDestroy(2000);
		base.TearDown();
	}

	[Test]
	public void the_file_is_not_deleted() {
		Assert.IsTrue(File.Exists(Filename));
	}
}
