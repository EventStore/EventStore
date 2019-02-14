using System;
using System.Threading.Tasks;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.Messages;
using EventStore.ClientAPI.SystemData;

namespace EventStore.ClientAPI.ClientOperations {
	internal class CommitTransactionOperation : OperationBase<WriteResult, ClientMessage.TransactionCommitCompleted> {
		private readonly bool _requireMaster;
		private readonly long _transactionId;

		public CommitTransactionOperation(ILogger log, TaskCompletionSource<WriteResult> source,
			bool requireMaster, long transactionId, UserCredentials userCredentials)
			: base(log, source, TcpCommand.TransactionCommit, TcpCommand.TransactionCommitCompleted, userCredentials) {
			_requireMaster = requireMaster;
			_transactionId = transactionId;
		}

		protected override object CreateRequestDto() {
			return new ClientMessage.TransactionCommit(_transactionId, _requireMaster);
		}

		protected override InspectionResult InspectResponse(ClientMessage.TransactionCommitCompleted response) {
			switch (response.Result) {
				case ClientMessage.OperationResult.Success:
					Succeed();
					return new InspectionResult(InspectionDecision.EndOperation, "Success");
				case ClientMessage.OperationResult.PrepareTimeout:
					return new InspectionResult(InspectionDecision.Retry, "PrepareTimeout");
				case ClientMessage.OperationResult.CommitTimeout:
					return new InspectionResult(InspectionDecision.Retry, "CommitTimeout");
				case ClientMessage.OperationResult.ForwardTimeout:
					return new InspectionResult(InspectionDecision.Retry, "ForwardTimeout");
				case ClientMessage.OperationResult.WrongExpectedVersion:
					var err = string.Format(
						"Commit transaction failed due to WrongExpectedVersion. TransactionID: {0}.", _transactionId);
					Fail(new WrongExpectedVersionException(err));
					return new InspectionResult(InspectionDecision.EndOperation, "WrongExpectedVersion");
				case ClientMessage.OperationResult.StreamDeleted:
					Fail(new StreamDeletedException());
					return new InspectionResult(InspectionDecision.EndOperation, "StreamDeleted");
				case ClientMessage.OperationResult.InvalidTransaction:
					Fail(new InvalidTransactionException());
					return new InspectionResult(InspectionDecision.EndOperation, "InvalidTransaction");
				case ClientMessage.OperationResult.AccessDenied:
					Fail(new AccessDeniedException("Write access denied."));
					return new InspectionResult(InspectionDecision.EndOperation, "AccessDenied");
				default:
					throw new Exception(string.Format("Unexpected OperationResult: {0}.", response.Result));
			}
		}

		protected override WriteResult TransformResponse(ClientMessage.TransactionCommitCompleted response) {
			return new WriteResult(response.LastEventNumber,
				new Position(response.PreparePosition ?? -1, response.CommitPosition ?? -1));
		}

		public override string ToString() {
			return string.Format("TransactionId: {0}", _transactionId);
		}
	}
}
