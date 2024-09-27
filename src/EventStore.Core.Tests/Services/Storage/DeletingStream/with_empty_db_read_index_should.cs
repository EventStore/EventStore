// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.DeletingStream {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class with_empty_db_read_index_should<TLogFormat, TStreamId> : ReadIndexTestScenario<TLogFormat, TStreamId> {
		protected override void WriteTestScenario() {
		}

		[Test]
		public void indicate_that_any_stream_is_not_deleted() {
			Assert.That(ReadIndex.IsStreamDeleted("X"), Is.False);
			Assert.That(ReadIndex.IsStreamDeleted("YY"), Is.False);
			Assert.That(ReadIndex.IsStreamDeleted("ZZZ"), Is.False);
		}
	}
}
