using System;
using System.Security.Claims;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Storage.ReaderIndex;

namespace EventStore.Core.Services.RequestManager.Managers {
	public class TransactionStart : RequestManagerBase {
		private readonly string _streamId;
		private readonly bool _betterOrdering;
		private readonly ClaimsPrincipal _user;

		public TransactionStart(
					IPublisher publisher,
					TimeSpan timeout,
					IEnvelope clientResponseEnvelope,
					Guid internalCorrId,
					Guid clientCorrId,
					string streamId,
					bool betterOrdering,
					long expectedVersion,
					ClaimsPrincipal user,
					CommitSource commitSource)
			: base(
					 publisher,
					 timeout,
					 clientResponseEnvelope,
					 internalCorrId,
					 clientCorrId,
					 expectedVersion,
					 commitSource,
					 prepareCount: 1) {
			_streamId = streamId;
			_betterOrdering = betterOrdering;
			_user = user;
		}

		protected override Message AccessRequestMsg =>
				new StorageMessage.CheckStreamAccess(
						WriteReplyEnvelope,
						InternalCorrId,
						_streamId,
						null,
						StreamAccessType.Write,
						_user,
						_betterOrdering);

		protected override Message WriteRequestMsg =>
			new StorageMessage.WriteTransactionStart(
					InternalCorrId,
					WriteReplyEnvelope,
					_streamId,
					ExpectedVersion,
					LiveUntil);

		protected override void AllEventsWritten() {
			if (CommitSource.ReplicationPosition >= LastEventPosition) {
				Committed();
			} else if (!Registered) {
				CommitSource.NotifyFor(LastEventPosition, Committed, CommitLevel.Replicated);
				Registered = true;
			}
		}

		protected override Message ClientSuccessMsg =>
			 new ClientMessage.TransactionStartCompleted(
						ClientCorrId,
						TransactionId,
						OperationResult.Success,
						null);

		protected override Message ClientFailMsg =>
			 new ClientMessage.TransactionStartCompleted(
						ClientCorrId,
						TransactionId,
						Result,
						FailureMessage);

	}
}
