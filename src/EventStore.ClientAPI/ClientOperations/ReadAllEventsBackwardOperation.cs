using System;
using System.Threading.Tasks;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.Messages;
using EventStore.ClientAPI.SystemData;

namespace EventStore.ClientAPI.ClientOperations {
	internal class
		ReadAllEventsBackwardOperation : OperationBase<AllEventsSlice, ClientMessage.ReadAllEventsCompleted> {
		private readonly Position _position;
		private readonly int _maxCount;
		private readonly bool _resolveLinkTos;
		private readonly bool _requireMaster;

		public ReadAllEventsBackwardOperation(ILogger log, TaskCompletionSource<AllEventsSlice> source,
			Position position, int maxCount, bool resolveLinkTos, bool requireMaster,
			UserCredentials userCredentials)
			: base(log, source, TcpCommand.ReadAllEventsBackward, TcpCommand.ReadAllEventsBackwardCompleted,
				userCredentials) {
			_position = position;
			_maxCount = maxCount;
			_resolveLinkTos = resolveLinkTos;
			_requireMaster = requireMaster;
		}

		protected override object CreateRequestDto() {
			return new ClientMessage.ReadAllEvents(_position.CommitPosition, _position.PreparePosition, _maxCount,
				_resolveLinkTos, _requireMaster);
		}

		protected override InspectionResult InspectResponse(ClientMessage.ReadAllEventsCompleted response) {
			switch (response.Result) {
				case ClientMessage.ReadAllEventsCompleted.ReadAllResult.Success:
					Succeed();
					return new InspectionResult(InspectionDecision.EndOperation, "Success");
				case ClientMessage.ReadAllEventsCompleted.ReadAllResult.Error:
					Fail(new ServerErrorException(
						string.IsNullOrEmpty(response.Error) ? "<no message>" : response.Error));
					return new InspectionResult(InspectionDecision.EndOperation, "Error");
				case ClientMessage.ReadAllEventsCompleted.ReadAllResult.AccessDenied:
					Fail(new AccessDeniedException("Read access denied for $all."));
					return new InspectionResult(InspectionDecision.EndOperation, "AccessDenied");
				default:
					throw new Exception(string.Format("Unexpected ReadAllResult: {0}.", response.Result));
			}
		}

		protected override AllEventsSlice TransformResponse(ClientMessage.ReadAllEventsCompleted response) {
			return new AllEventsSlice(ReadDirection.Backward,
				new Position(response.CommitPosition, response.PreparePosition),
				new Position(response.NextCommitPosition, response.NextPreparePosition),
				response.Events);
		}

		public override string ToString() {
			return string.Format("Position: {0}, MaxCount: {1}, ResolveLinkTos: {2}, RequireMaster: {3}",
				_position, _maxCount, _resolveLinkTos, _requireMaster);
		}
	}
}
