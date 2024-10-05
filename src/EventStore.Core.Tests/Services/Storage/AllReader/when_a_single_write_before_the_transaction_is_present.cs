// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System.Threading.Tasks;
using EventStore.Core.Tests.TransactionLog.Scavenging.Helpers;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Storage.AllReader;

[TestFixture(typeof(LogFormat.V2), typeof(string))]
[TestFixture(typeof(LogFormat.V3), typeof(uint), Ignore = "Explicit transactions are not supported yet by Log V3")]
public class when_a_single_write_before_the_transaction_is_present<TLogFormat, TStreamId> : RepeatableDbTestScenario<TLogFormat, TStreamId> {
	[Test]
	public async Task should_be_able_to_read_the_transactional_writes_when_the_commit_is_present() {
		await CreateDb([
			Rec.Prepare(0, "single_write_stream_id_1", prepareFlags: PrepareFlags.Data | PrepareFlags.IsCommitted),
			Rec.TransSt(1, "transaction_stream_id"),
			Rec.Prepare(1, "transaction_stream_id"),
			Rec.TransEnd(1, "transaction_stream_id"),
			Rec.Prepare(2, "single_write_stream_id_2", prepareFlags: PrepareFlags.SingleWrite | PrepareFlags.IsCommitted),
			Rec.Prepare(3, "single_write_stream_id_3", prepareFlags: PrepareFlags.SingleWrite | PrepareFlags.IsCommitted),
			Rec.Prepare(4, "single_write_stream_id_4", prepareFlags: PrepareFlags.SingleWrite | PrepareFlags.IsCommitted)
			]);

		var firstRead = ReadIndex.ReadAllEventsForward(new Data.TFPos(0, 0), 10);

		Assert.AreEqual(4, firstRead.Records.Count);
		Assert.AreEqual("single_write_stream_id_1", firstRead.Records[0].Event.EventStreamId);
		Assert.AreEqual("single_write_stream_id_2", firstRead.Records[1].Event.EventStreamId);
		Assert.AreEqual("single_write_stream_id_3", firstRead.Records[2].Event.EventStreamId);
		Assert.AreEqual("single_write_stream_id_4", firstRead.Records[3].Event.EventStreamId);

		await CreateDb([
			Rec.Prepare(0, "single_write_stream_id_1", prepareFlags: PrepareFlags.Data | PrepareFlags.IsCommitted),
			Rec.TransSt(1, "transaction_stream_id"),
			Rec.Prepare(1, "transaction_stream_id"),
			Rec.TransEnd(1, "transaction_stream_id"),
			Rec.Prepare(2, "single_write_stream_id_2", prepareFlags: PrepareFlags.SingleWrite | PrepareFlags.IsCommitted),
			Rec.Prepare(3, "single_write_stream_id_3", prepareFlags: PrepareFlags.SingleWrite | PrepareFlags.IsCommitted),
			Rec.Prepare(4, "single_write_stream_id_4", prepareFlags: PrepareFlags.SingleWrite | PrepareFlags.IsCommitted),
			Rec.Commit(1, "transaction_stream_id")
			]);

		var transactionRead = ReadIndex.ReadAllEventsForward(firstRead.NextPos, 10);

		Assert.AreEqual(1, transactionRead.Records.Count);
		Assert.AreEqual("transaction_stream_id", transactionRead.Records[0].Event.EventStreamId);
	}
}
