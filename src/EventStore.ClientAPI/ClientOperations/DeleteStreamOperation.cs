using System;
using System.Threading.Tasks;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.Messages;
using EventStore.ClientAPI.SystemData;

namespace EventStore.ClientAPI.ClientOperations {
	internal class DeleteStreamOperation : OperationBase<DeleteResult, ClientMessage.DeleteStreamCompleted> {
		private readonly bool _requireMaster;
		private readonly string _stream;
		private readonly long _expectedVersion;
		private readonly bool _hardDelete;

		public DeleteStreamOperation(ILogger log, TaskCompletionSource<DeleteResult> source,
			bool requireMaster, string stream, long expectedVersion, bool hardDelete,
			UserCredentials userCredentials)
			: base(log, source, TcpCommand.DeleteStream, TcpCommand.DeleteStreamCompleted, userCredentials) {
			_requireMaster = requireMaster;
			_stream = stream;
			_expectedVersion = expectedVersion;
			_hardDelete = hardDelete;
		}

		protected override object CreateRequestDto() {
			return new ClientMessage.DeleteStream(_stream, _expectedVersion, _requireMaster, _hardDelete);
		}

		protected override InspectionResult InspectResponse(ClientMessage.DeleteStreamCompleted response) {
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
						"Delete stream failed due to WrongExpectedVersion. Stream: {0}, Expected version: {1}.",
						_stream, _expectedVersion);
					Fail(new WrongExpectedVersionException(err));
					return new InspectionResult(InspectionDecision.EndOperation, "WrongExpectedVersion");
				case ClientMessage.OperationResult.StreamDeleted:
					Fail(new StreamDeletedException(_stream));
					return new InspectionResult(InspectionDecision.EndOperation, "StreamDeleted");
				case ClientMessage.OperationResult.InvalidTransaction:
					Fail(new InvalidTransactionException());
					return new InspectionResult(InspectionDecision.EndOperation, "InvalidTransaction");
				case ClientMessage.OperationResult.AccessDenied:
					Fail(new AccessDeniedException(string.Format("Write access denied for stream '{0}'.", _stream)));
					return new InspectionResult(InspectionDecision.EndOperation, "AccessDenied");
				default:
					throw new Exception(string.Format("Unexpected OperationResult: {0}.", response.Result));
			}
		}

		protected override DeleteResult TransformResponse(ClientMessage.DeleteStreamCompleted response) {
			return new DeleteResult(new Position(response.PreparePosition ?? -1, response.CommitPosition ?? -1));
		}

		public override string ToString() {
			return string.Format("Stream: {0}, ExpectedVersion: {1}.", _stream, _expectedVersion);
		}
	}
}
