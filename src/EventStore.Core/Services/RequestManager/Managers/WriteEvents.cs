using System;
using System.Security.Principal;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Commit;
using EventStore.Core.Services.Storage.ReaderIndex;

namespace EventStore.Core.Services.RequestManager.Managers {
	public class WriteEvents : RequestManagerBase {
		private readonly string _streamId;
		private readonly bool _betterOrdering;
		private readonly IPrincipal _user;
		private readonly Event[] _events;
		private readonly StreamAccessType _accessType;
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
					ICommitSource commitSource)
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
			//this seems like it should work, but really really doesn't
			//_accessType = SystemStreams.IsMetastream(streamId) ? StreamAccessType.MetaWrite : StreamAccessType.Write;
			_accessType =StreamAccessType.Write;
			_betterOrdering = betterOrdering;
			_user = user;
			_events = events;
		}

		protected override Message AccessRequestMsg =>				
				new StorageMessage.CheckStreamAccess(
						WriteReplyEnvelope, 
						InternalCorrId, 
						_streamId, 
						null, 
						_accessType, 
						_user, 
						_betterOrdering);

		protected override Message WriteRequestMsg =>
			new StorageMessage.WritePrepares(
					InternalCorrId,
					WriteReplyEnvelope,
					_streamId,
					ExpectedVersion,
					_events,
					LiveUntil);


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
