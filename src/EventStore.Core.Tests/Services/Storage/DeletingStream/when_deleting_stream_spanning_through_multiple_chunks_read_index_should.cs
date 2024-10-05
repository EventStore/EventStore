// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.DeletingStream;

[TestFixture(typeof(LogFormat.V2), typeof(string))]
[TestFixture(typeof(LogFormat.V3), typeof(uint))]
public class when_deleting_stream_spanning_through_multiple_chunks_read_index_should<TLogFormat, TStreamId> : ReadIndexTestScenario<TLogFormat, TStreamId> {
	protected override async ValueTask WriteTestScenario(CancellationToken token) {
		await WriteSingleEvent("ES", 0, new string('.', 3000), token: token);
		await WriteSingleEvent("ES", 1, new string('.', 3000), token: token);
		await WriteSingleEvent("ES", 2, new string('.', 3000), token: token);
		await WriteSingleEvent("ES", 3, new string('.', 3000), retryOnFail: true, token: token); // chunk 2
		await WriteSingleEvent("ES", 4, new string('.', 3000), token: token);
		await WriteSingleEvent("ES", 5, new string('.', 3000), token: token);
		await WriteSingleEvent("ES", 6, new string('.', 3000), retryOnFail: true, token: token); // chunk 3

		await WriteDelete("ES", token);
	}

	[Test]
	public void indicate_that_stream_is_deleted() {
		Assert.That(ReadIndex.IsStreamDeleted("ES"));
	}

	[Test]
	public void indicate_that_nonexisting_stream_with_same_hash_is_not_deleted() {
		Assert.That(ReadIndex.IsStreamDeleted("ZZ"), Is.False);
	}

	[Test]
	public void indicate_that_nonexisting_stream_with_different_hash_is_not_deleted() {
		Assert.That(ReadIndex.IsStreamDeleted("XXX"), Is.False);
	}
}
