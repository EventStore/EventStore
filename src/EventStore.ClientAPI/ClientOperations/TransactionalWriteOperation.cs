using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.Messages;
using EventStore.ClientAPI.SystemData;

namespace EventStore.ClientAPI.ClientOperations {
	internal class TransactionalWriteOperation : OperationBase<object, ClientMessage.TransactionWriteCompleted> {
		private readonly bool _requireMaster;
		private readonly long _transactionId;
		private readonly IEnumerable<EventData> _events;

		public TransactionalWriteOperation(ILogger log, TaskCompletionSource<object> source,
			bool requireMaster, long transactionId, IEnumerable<EventData> events,
			UserCredentials userCredentials)
			: base(log, source, TcpCommand.TransactionWrite, TcpCommand.TransactionWriteCompleted, userCredentials) {
			_requireMaster = requireMaster;
			_transactionId = transactionId;
			_events = events;
		}

		protected override object CreateRequestDto() {
			var dtos = _events.Select(x =>
					new ClientMessage.NewEvent(x.EventId.ToByteArray(), x.Type, x.IsJson ? 1 : 0, 0, x.Data,
						x.Metadata))
				.ToArray();
			return new ClientMessage.TransactionWrite(_transactionId, dtos, _requireMaster);
		}

		protected override InspectionResult InspectResponse(ClientMessage.TransactionWriteCompleted response) {
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
				case ClientMessage.OperationResult.AccessDenied:
					Fail(new AccessDeniedException("Write access denied."));
					return new InspectionResult(InspectionDecision.EndOperation, "AccessDenied");
				default:
					throw new Exception(string.Format("Unexpected OperationResult: {0}.", response.Result));
			}
		}

		protected override object TransformResponse(ClientMessage.TransactionWriteCompleted response) {
			return null;
		}

		public override string ToString() {
			return string.Format("TransactionId: {0}", _transactionId);
		}
	}
}
