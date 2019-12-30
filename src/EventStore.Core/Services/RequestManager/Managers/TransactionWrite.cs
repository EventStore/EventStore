using System;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;

namespace EventStore.Core.Services.RequestManager.Managers {
	public class TransactionWrite : RequestManagerBase {
		private readonly Event[] _events;
		public TransactionWrite(
					IPublisher publisher,
					TimeSpan timeout,
					IEnvelope clientResponseEnvelope,
					Guid internalCorrId,
					Guid clientCorrId,
					Event[] events,
					long transactionId,
					long currentLogPosition = 0)
			: base(
					 publisher,
					 timeout,
					 clientResponseEnvelope,
					 internalCorrId,
					 clientCorrId,
					 expectedVersion: -1,
					 prepareCount: events.Length,
					 transactionId,
					 completeOnLogCommitted: true,
					 currentLogPosition: currentLogPosition) {
			_events = events;
		}
		protected override Message AccessRequestMsg => null; //we don't have a user on the tx write message

		protected override Message WriteRequestMsg =>
			new StorageMessage.WriteTransactionData(
					InternalCorrId,
					WriteReplyEnvelope,
					TransactionId,
					_events);


		protected override Message ClientSuccessMsg =>
			 new ClientMessage.TransactionWriteCompleted(
						ClientCorrId,
						TransactionId,
						OperationResult.Success,
						null);
		protected override Message ClientFailMsg =>
			 new ClientMessage.TransactionWriteCompleted(
						ClientCorrId,
						TransactionId,
						Result,
						FailureMessage);
	}
}
