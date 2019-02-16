using System;
using System.Threading.Tasks;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.Messages;
using EventStore.ClientAPI.SystemData;

namespace EventStore.ClientAPI.ClientOperations {
	internal class
		ReadStreamEventsBackwardOperation : OperationBase<StreamEventsSlice, ClientMessage.ReadStreamEventsCompleted> {
		private readonly string _stream;
		private readonly long _fromEventNumber;
		private readonly int _maxCount;
		private readonly bool _resolveLinkTos;
		private readonly bool _requireMaster;

		public ReadStreamEventsBackwardOperation(ILogger log, TaskCompletionSource<StreamEventsSlice> source,
			string stream, long fromEventNumber, int maxCount, bool resolveLinkTos,
			bool requireMaster, UserCredentials userCredentials)
			: base(log, source, TcpCommand.ReadStreamEventsBackward, TcpCommand.ReadStreamEventsBackwardCompleted,
				userCredentials) {
			_stream = stream;
			_fromEventNumber = fromEventNumber;
			_maxCount = maxCount;
			_resolveLinkTos = resolveLinkTos;
			_requireMaster = requireMaster;
		}

		protected override object CreateRequestDto() {
			return new ClientMessage.ReadStreamEvents(_stream, _fromEventNumber, _maxCount, _resolveLinkTos,
				_requireMaster);
		}

		protected override InspectionResult InspectResponse(ClientMessage.ReadStreamEventsCompleted response) {
			switch (response.Result) {
				case ClientMessage.ReadStreamEventsCompleted.ReadStreamResult.Success:
					Succeed();
					return new InspectionResult(InspectionDecision.EndOperation, "Success");
				case ClientMessage.ReadStreamEventsCompleted.ReadStreamResult.StreamDeleted:
					Succeed();
					return new InspectionResult(InspectionDecision.EndOperation, "StreamDeleted");
				case ClientMessage.ReadStreamEventsCompleted.ReadStreamResult.NoStream:
					Succeed();
					return new InspectionResult(InspectionDecision.EndOperation, "NoStream");
				case ClientMessage.ReadStreamEventsCompleted.ReadStreamResult.Error:
					Fail(new ServerErrorException(
						string.IsNullOrEmpty(response.Error) ? "<no message>" : response.Error));
					return new InspectionResult(InspectionDecision.EndOperation, "Error");
				case ClientMessage.ReadStreamEventsCompleted.ReadStreamResult.AccessDenied:
					Fail(new AccessDeniedException(string.Format("Read access denied for stream '{0}'.", _stream)));
					return new InspectionResult(InspectionDecision.EndOperation, "AccessDenied");
				default:
					throw new Exception(string.Format("Unexpected ReadStreamResult: {0}.", response.Result));
			}
		}

		protected override StreamEventsSlice TransformResponse(ClientMessage.ReadStreamEventsCompleted response) {
			return new StreamEventsSlice(StatusCode.Convert(response.Result),
				_stream,
				_fromEventNumber,
				ReadDirection.Backward,
				response.Events,
				response.NextEventNumber,
				response.LastEventNumber,
				response.IsEndOfStream);
		}

		public override string ToString() {
			return string.Format(
				"Stream: {0}, FromEventNumber: {1}, MaxCount: {2}, ResolveLinkTos: {3}, RequireMaster: {4}",
				_stream, _fromEventNumber, _maxCount, _resolveLinkTos, _requireMaster);
		}
	}
}
