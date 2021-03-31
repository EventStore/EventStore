using System;
using System.Threading;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;

namespace EventStore.Core.Services.RequestManager.Managers {
	public class AppendEvents : RequestManagerBase {
		private readonly string _streamId;
		private readonly Event[] _events;
		private readonly CancellationToken _cancellationToken;
		private readonly CommitLevel _commitLevel;
		public AppendEvents(IPublisher publisher,
			TimeSpan timeout,
			IEnvelope clientResponseEnvelope,
			Guid internalCorrId,
			Guid clientCorrId,
			string streamId,
			long expectedVersion,
			Event[] events,
			CommitSource commitSource,
			CommitLevel commitLevel,
			CancellationToken cancellationToken = default)
			: base(
					 publisher,
					 timeout,
					 clientResponseEnvelope,
					 internalCorrId,
					 clientCorrId,
					 expectedVersion,
					 commitSource,
					 prepareCount: 0,
					 waitForCommit: true) {
			_streamId = streamId;
			_events = events;
			_cancellationToken = cancellationToken;
			//todo: min stream name length?						
			if (commitLevel == CommitLevel.PerStream) {
				_commitLevel = _streamId.Length >= 3 ? ParsePerStreamCommitLevel(_streamId.AsSpan(0, 3)) : CommitLevel.Indexed;				
			} else {
				_commitLevel = _streamId.AsSpan(0, 1)[0] == '$' ? CommitLevel.Indexed : commitLevel; //systems streams '$' must always be read commited
			}
		}

		private CommitLevel ParsePerStreamCommitLevel(ReadOnlySpan<char> streamPrefix) {
			if (streamPrefix[0] != '$') { return CommitLevel.Indexed; } // Not '$' standard user stream
			if (streamPrefix[1] != '!') { return CommitLevel.Indexed; } // Not '$!' system stream
			if (streamPrefix[2] != '!') { return CommitLevel.Replicated; } // "$!" Replicated commit requested
			if (streamPrefix[2] == '!') { return CommitLevel.Leader; } // "$!!" Written commit requested
			return CommitLevel.Indexed; // we shuold not get here 
		}
		protected override Message WriteRequestMsg =>
			new StorageMessage.WritePrepares(
					InternalCorrId,
					WriteReplyEnvelope,
					_streamId,
					ExpectedVersion,
					_events,
					_cancellationToken);
		protected override void AllEventsWritten() {
			if (!Registered) {
				switch (_commitLevel) {
					case CommitLevel.Leader:
						Committed();
						break;
					case CommitLevel.Replicated:
						CommitSource.RegisterReplicated(LastEventPosition, Committed);
						break;
					case CommitLevel.Indexed:
						CommitSource.RegisterIndexed(LastEventPosition, Committed);
						break;
				}
				Registered = true;
			}
		}

		protected override Message ClientSuccessMsg =>
			 new ClientMessage.WriteEventsCompleted(
				 ClientCorrId,
				 FirstEventNumber,
				 LastEventNumber,
				 CommitPosition,  //not technically correct, but matches current behavior correctly
				 CommitPosition);

		protected override Message ClientFailMsg =>
			 new ClientMessage.WriteEventsCompleted(
				 ClientCorrId,
				 Result,
				 FailureMessage,
				 FailureCurrentVersion);
	}
}
