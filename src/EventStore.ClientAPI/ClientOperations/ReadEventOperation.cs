using System;
using System.Threading.Tasks;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.Messages;
using EventStore.ClientAPI.SystemData;

namespace EventStore.ClientAPI.ClientOperations {
	internal class ReadEventOperation : OperationBase<EventReadResult, ClientMessage.ReadEventCompleted> {
		private readonly string _stream;
		private readonly long _eventNumber;
		private readonly bool _resolveLinkTo;
		private readonly bool _requireMaster;

		public ReadEventOperation(ILogger log, TaskCompletionSource<EventReadResult> source,
			string stream, long eventNumber, bool resolveLinkTo, bool requireMaster, UserCredentials userCredentials)
			: base(log, source, TcpCommand.ReadEvent, TcpCommand.ReadEventCompleted, userCredentials) {
			_stream = stream;
			_eventNumber = eventNumber;
			_resolveLinkTo = resolveLinkTo;
			_requireMaster = requireMaster;
		}

		protected override object CreateRequestDto() {
			return new ClientMessage.ReadEvent(_stream, _eventNumber, _resolveLinkTo, _requireMaster);
		}

		protected override InspectionResult InspectResponse(ClientMessage.ReadEventCompleted response) {
			switch (response.Result) {
				case ClientMessage.ReadEventCompleted.ReadEventResult.Success:
					Succeed();
					return new InspectionResult(InspectionDecision.EndOperation, "Success");
				case ClientMessage.ReadEventCompleted.ReadEventResult.NotFound:
					Succeed();
					return new InspectionResult(InspectionDecision.EndOperation, "NotFound");
				case ClientMessage.ReadEventCompleted.ReadEventResult.NoStream:
					Succeed();
					return new InspectionResult(InspectionDecision.EndOperation, "NoStream");
				case ClientMessage.ReadEventCompleted.ReadEventResult.StreamDeleted:
					Succeed();
					return new InspectionResult(InspectionDecision.EndOperation, "StreamDeleted");
				case ClientMessage.ReadEventCompleted.ReadEventResult.Error:
					Fail(new ServerErrorException(
						string.IsNullOrEmpty(response.Error) ? "<no message>" : response.Error));
					return new InspectionResult(InspectionDecision.EndOperation, "Error");
				case ClientMessage.ReadEventCompleted.ReadEventResult.AccessDenied:
					Fail(new AccessDeniedException(string.Format("Read access denied for stream '{0}'.", _stream)));
					return new InspectionResult(InspectionDecision.EndOperation, "AccessDenied");
				default:
					throw new Exception(string.Format("Unexpected ReadEventResult: {0}.", response.Result));
			}
		}

		protected override EventReadResult TransformResponse(ClientMessage.ReadEventCompleted response) {
			return new EventReadResult(Convert(response.Result), _stream, _eventNumber, response.Event);
		}


		private static EventReadStatus Convert(ClientMessage.ReadEventCompleted.ReadEventResult result) {
			switch (result) {
				case ClientMessage.ReadEventCompleted.ReadEventResult.Success:
					return EventReadStatus.Success;
				case ClientMessage.ReadEventCompleted.ReadEventResult.NotFound:
					return EventReadStatus.NotFound;
				case ClientMessage.ReadEventCompleted.ReadEventResult.NoStream:
					return EventReadStatus.NoStream;
				case ClientMessage.ReadEventCompleted.ReadEventResult.StreamDeleted:
					return EventReadStatus.StreamDeleted;
				default:
					throw new Exception(string.Format("Unexpected ReadEventResult: {0}.", result));
			}
		}

		public override string ToString() {
			return string.Format("Stream: {0}, EventNumber: {1}, ResolveLinkTo: {2}, RequireMaster: {3}",
				_stream, _eventNumber, _resolveLinkTo, _requireMaster);
		}
	}
}
