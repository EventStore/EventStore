// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.DeletingStream {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class when_deleting_stream_with_2_hash_collisions_and_mixed_order_read_index_should<TLogFormat, TStreamId> : ReadIndexTestScenario<TLogFormat, TStreamId> {
		protected override async ValueTask WriteTestScenario(CancellationToken token) {
			await WriteSingleEvent("S1", 0, "bla1", token: token);
			await WriteSingleEvent("S1", 1, "bla1", token: token);
			await WriteSingleEvent("S2", 0, "bla1", token: token);
			await WriteSingleEvent("S2", 1, "bla1", token: token);
			await WriteSingleEvent("S1", 2, "bla1", token: token);
			await WriteSingleEvent("S3", 0, "bla1", token: token);

			await WriteDelete("S1", token);
		}

		[Test]
		public void indicate_that_stream_is_deleted() {
			Assert.That(ReadIndex.IsStreamDeleted("S1"));
		}

		[Test]
		public void indicate_that_other_streams_with_same_hash_are_not_deleted() {
			Assert.That(ReadIndex.IsStreamDeleted("S2"), Is.False);
			Assert.That(ReadIndex.IsStreamDeleted("S3"), Is.False);
		}

		[Test]
		public void indicate_that_not_existing_stream_with_same_hash_is_not_deleted() {
			Assert.That(ReadIndex.IsStreamDeleted("XX"), Is.False);
		}

		[Test]
		public void indicate_that_not_existing_stream_with_different_hash_is_not_deleted() {
			Assert.That(ReadIndex.IsStreamDeleted("XXX"), Is.False);
		}
	}
}
