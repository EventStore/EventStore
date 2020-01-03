using System;
using EventStore.Core.Bus;
using EventStore.Core.Messages;

namespace EventStore.Core.Services.Commit {
	public interface ICommitSource:
		IHandle<CommitMessage.CommittedTo>,
		IHandle<CommitMessage.LogCommittedTo> {
		long CommitPosition { get; }
		long LogCommitPosition { get; }

		void NotifyCommitFor(long postition, Action target);
		void NotifyLogCommitFor(long postition, Action target);
	}
}
