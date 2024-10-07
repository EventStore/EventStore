// Copyright (c) Event Store Ltd and/or licensed to Event Store Ltd under one or more agreements.
// Event Store Ltd licenses this file to you under the Event Store License v2 (see LICENSE.md).

using System;
using EventStore.Core.Messages;
using EventStore.Core.TransactionLog.Checkpoint;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.IndexCommitter;

[TestFixture(typeof(LogFormat.V2), typeof(string))]
[TestFixture(typeof(LogFormat.V3), typeof(uint))]
public class when_index_committer_service_receives_replicated_to_prepare_post_position<TLogFormat, TStreamId> : with_index_committer_service<TLogFormat, TStreamId> {
	private readonly Guid _correlationId = Guid.NewGuid();
	private readonly long _logPrePosition = 4000;
	private readonly long _logPostPosition = 4001;

	public override void TestFixtureSetUp() {
		ReplicationCheckpoint = new InMemoryCheckpoint(0);
		base.TestFixtureSetUp();
	}
	public override void Given() { }
	public override void When() {

		AddPendingPrepare(_logPrePosition, _logPostPosition);
		Service.Handle(new StorageMessage.CommitAck(_correlationId, _logPrePosition, _logPrePosition, 0, 0));
		ReplicationCheckpoint.Write(_logPostPosition);
		ReplicationCheckpoint.Flush();
		Service.Handle(new ReplicationTrackingMessage.ReplicatedTo(_logPostPosition));
	}

	[Test]
	public void commit_replicated_message_should_have_been_published() {
		AssertEx.IsOrBecomesTrue(() => 1 == CommitReplicatedMgs.Count);
		Assert.True(CommitReplicatedMgs.TryDequeue(out var msg));
		Assert.AreEqual(_logPrePosition, msg.TransactionPosition);
	}
	[Test]
	public void index_written_message_should_have_been_published() {
		AssertEx.IsOrBecomesTrue(() => 1 == IndexWrittenMgs.Count);
		Assert.True(IndexWrittenMgs.TryDequeue(out var msg));
		Assert.AreEqual(_logPrePosition, msg.LogPosition);
	}
	[Test]
	public void index_should_have_been_updated() {
		AssertEx.IsOrBecomesTrue(() => 1 == IndexCommitter.CommittedPrepares.Count);
		Assert.True(IndexCommitter.CommittedPrepares.TryDequeue(out var msg));
		Assert.AreEqual(_logPrePosition, msg.LogPosition);
	}
}
