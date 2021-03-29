using System;
using System.Threading;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;

namespace EventStore.Core.Services.RequestManager.Managers {
	public class TransactionStart : RequestManagerBase {
		private readonly string _streamId;
		
		public TransactionStart(
					IPublisher publisher,
					long startOffset,
					TimeSpan timeout,
					IEnvelope clientResponseEnvelope,
					Guid internalCorrId,
					Guid clientCorrId,
					string streamId,
					long expectedVersion,
					CommitSource commitSource)
			: base(
					 publisher,
					 startOffset,
					 timeout,
					 clientResponseEnvelope,
					 internalCorrId,
					 clientCorrId,
					 expectedVersion,
					 commitSource,
					 prepareCount: 1) {
			_streamId = streamId;
			Result = OperationResult.PrepareTimeout; // we need an unknown here
		}
		
		protected override Message WriteRequestMsg =>
			new StorageMessage.WriteTransactionStart(
					InternalCorrId,
					WriteReplyEnvelope,
					_streamId,
					ExpectedVersion,
					DateTime.UtcNow + Timeout);

		protected override void AllEventsWritten() {
			if (!Registered) {
				var phase = Interlocked.Increment(ref Phase);
				CommitSource.NotifyOnReplicated(LastEventPosition, Committed);
				CommitSource.NotifyAfter(Timeout, () => PhaseTimeout(phase));
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
