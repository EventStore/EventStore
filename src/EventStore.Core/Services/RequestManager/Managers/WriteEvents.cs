using System;
using System.Security.Principal;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;

namespace EventStore.Core.Services.RequestManager.Managers {
	public class WriteEvents : RequestManagerBase {
		private readonly Event[] _events;
		public WriteEvents(
					IPublisher publisher,
					TimeSpan timeout,
					IEnvelope clientResponseEnvelope,
					Guid internalCorrId,
					Guid clientCorrId,
					string streamId,
					bool betterOrdering,
					long expectedVersion,
					IPrincipal user,
					Event[] events,
					long currentCommitPosition = 0)
			: base(
					 publisher,
					 timeout,
					 clientResponseEnvelope,
					 internalCorrId,
					 clientCorrId,
					 streamId,
					 betterOrdering,
					 expectedVersion,
					 user,
					 prepareCount: 0,
					 waitForCommit: true,
					 currentLogPosition: currentCommitPosition) {
			_events = events;
		}

		public override Message WriteRequestMsg =>
			new StorageMessage.WritePrepares(
					InternalCorrId,
					WriteReplyEnvelope,
					StreamId,
					ExpectedVersion,
					_events,
					LiveUntil);


		protected override Message ClientSuccessMsg =>
			 new ClientMessage.WriteEventsCompleted(
				 ClientCorrId,
				 FirstEventNumber,
				 LastEventNumber,
				 FirstPrepare,
				 CommitPosition);

		protected override Message ClientFailMsg =>
			 new ClientMessage.WriteEventsCompleted(
				 ClientCorrId,
				 Result,
				 FailureMessage,
				 FailureCurrentVersion);
	}
}
