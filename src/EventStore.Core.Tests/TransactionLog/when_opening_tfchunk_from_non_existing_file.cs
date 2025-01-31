// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.IO;
using EventStore.Core.Exceptions;
using EventStore.Core.TransactionLog.Chunks;
using EventStore.Core.TransactionLog.Chunks.TFChunk;
using EventStore.Core.Transforms.Identity;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog;

[TestFixture]
public class when_opening_tfchunk_from_non_existing_file : SpecificationWithFile {
	[Test]
	public void it_should_throw_a_file_not_found_exception() {
		Assert.ThrowsAsync<CorruptDatabaseException>(async () => await TFChunk.FromCompletedFile(new ChunkLocalFileSystem(Path.GetDirectoryName(Filename)), Filename, verifyHash: true,
			unbufferedRead: false, reduceFileCachePressure: false, tracker: new TFChunkTracker.NoOp(),
			getTransformFactory: _ => new IdentityChunkTransformFactory()));
	}
}
