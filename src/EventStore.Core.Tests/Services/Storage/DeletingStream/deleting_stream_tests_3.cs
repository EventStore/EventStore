// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.DeletingStream;

[TestFixture(typeof(LogFormat.V2), typeof(string))]
[TestFixture(typeof(LogFormat.V3), typeof(uint))]
public class
	when_deleting_streams_with_same_hash_spanning_through_multiple_chunks_in_db_with_1_stream_with_different_hash_read_index_should<TLogFormat, TStreamId> :
		ReadIndexTestScenario<TLogFormat, TStreamId> {
	protected override async ValueTask WriteTestScenario(CancellationToken token) {
		await WriteSingleEvent("ES1", 0, new string('.', 3000), token: token);
		await WriteSingleEvent("ES1", 1, new string('.', 3000), token: token);
		await WriteSingleEvent("ES2", 0, new string('.', 3000), token: token);

		await WriteSingleEvent("ES", 0, new string('.', 3000), retryOnFail: true, token: token); // chunk 2
		await WriteSingleEvent("ES", 1, new string('.', 3000), token: token);
		await WriteSingleEvent("ES1", 2, new string('.', 3000), token: token);

		await WriteSingleEvent("ES2", 1, new string('.', 3000), retryOnFail: true, token: token); // chunk 3
		await WriteSingleEvent("ES1", 3, new string('.', 3000), token: token);
		await WriteSingleEvent("ES1", 4, new string('.', 3000), token: token);

		await WriteSingleEvent("ES2", 2, new string('.', 3000), retryOnFail: true, token: token); // chunk 4
		await WriteSingleEvent("ES", 2, new string('.', 3000), token: token);
		await WriteSingleEvent("ES", 3, new string('.', 2500), token: token);

		await WriteDelete("ES1", token);
		await WriteDelete("ES2", token);
	}

	[Test]
	public void indicate_that_streams_are_deleted() {
		Assert.That(ReadIndex.IsStreamDeleted("ES1"));
		Assert.That(ReadIndex.IsStreamDeleted("ES2"));
	}

	[Test]
	public void indicate_that_nonexisting_stream_with_same_hash_is_not_deleted() {
		Assert.That(ReadIndex.IsStreamDeleted("XXX"), Is.False);
	}

	[Test]
	public void indicate_that_nonexisting_stream_with_different_hash_is_not_deleted() {
		Assert.That(ReadIndex.IsStreamDeleted("XXXX"), Is.False);
	}

	[Test]
	public void indicate_that_existing_stream_with_different_hash_is_not_deleted() {
		Assert.That(ReadIndex.IsStreamDeleted("ES"), Is.False);
	}
}
