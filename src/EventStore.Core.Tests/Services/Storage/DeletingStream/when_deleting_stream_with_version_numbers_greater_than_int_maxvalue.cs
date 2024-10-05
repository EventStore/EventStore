// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.DeletingStream {
	[TestFixture(typeof(LogFormat.V2), typeof(string))]
	[TestFixture(typeof(LogFormat.V3), typeof(uint))]
	public class when_deleting_stream_with_version_numbers_greater_than_int_maxvalue<TLogFormat, TStreamId> : ReadIndexTestScenario<TLogFormat, TStreamId> {
		long firstEventNumber = (long)int.MaxValue + 1;
		long secondEventNumber = (long)int.MaxValue + 2;
		long thirdEventNumber = (long)int.MaxValue + 3;

		protected override async ValueTask WriteTestScenario(CancellationToken token) {
			await WriteSingleEvent("ES", firstEventNumber, new string('.', 3000), token: token);
			await WriteSingleEvent("KEEP", firstEventNumber, new string('.', 3000), token: token);
			await WriteSingleEvent("KEEP", secondEventNumber, new string('.', 3000), token: token);
			await WriteSingleEvent("ES", secondEventNumber, new string('.', 3000), retryOnFail: true, token: token);
			await WriteSingleEvent("KEEP", thirdEventNumber, new string('.', 3000), token: token);
			await WriteSingleEvent("ES", thirdEventNumber, new string('.', 3000), token: token);

			await WriteDelete("ES", token);
		}

		[Test]
		public void indicate_that_stream_is_deleted() {
			Assert.That(ReadIndex.IsStreamDeleted("ES"));
		}

		[Test]
		public void indicate_that_other_stream_is_not_deleted() {
			Assert.IsFalse(ReadIndex.IsStreamDeleted("KEEP"));
		}
	}
}
