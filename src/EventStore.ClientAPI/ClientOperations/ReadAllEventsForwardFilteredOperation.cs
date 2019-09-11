using System;
using System.Threading.Tasks;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.Messages;
using EventStore.ClientAPI.SystemData;

namespace EventStore.ClientAPI.ClientOperations {
	internal class
		ReadAllEventsForwardFilteredOperation : OperationBase<AllEventsSlice,
			ClientMessage.ReadAllEventsFilteredCompleted> {
		private readonly Position _position;
		private readonly int _maxCount;
		private readonly bool _resolveLinkTos;
		private readonly bool _requireMaster;
		private readonly int _maxSearchWindow;
		private readonly ClientMessage.Filter _filter;

		public ReadAllEventsForwardFilteredOperation(ILogger log, TaskCompletionSource<AllEventsSlice> source,
			Position position, int maxCount, bool resolveLinkTos, bool requireMaster, int maxSearchWindow,
			ClientMessage.Filter filter,
			UserCredentials userCredentials)
			: base(log, source, TcpCommand.ReadAllEventsForwardFiltered,
				TcpCommand.ReadAllEventsForwardFilteredCompleted,
				userCredentials) {
			_position = position;
			_maxCount = maxCount;
			_resolveLinkTos = resolveLinkTos;
			_requireMaster = requireMaster;
			_maxSearchWindow = maxSearchWindow;
			_filter = filter;
		}

		protected override object CreateRequestDto() {
			return new ClientMessage.ReadAllEventsFiltered(_position.CommitPosition, _position.PreparePosition,
				_maxCount, _maxSearchWindow, _resolveLinkTos, _requireMaster, _filter);
		}

		protected override InspectionResult InspectResponse(ClientMessage.ReadAllEventsFilteredCompleted response) {
			switch (response.Result) {
				case ClientMessage.ReadAllEventsFilteredCompleted.ReadAllFilteredResult.Success:
					Succeed();
					return new InspectionResult(InspectionDecision.EndOperation, "Success");
				case ClientMessage.ReadAllEventsFilteredCompleted.ReadAllFilteredResult.Error:
					Fail(new ServerErrorException(
						string.IsNullOrEmpty(response.Error) ? "<no message>" : response.Error));
					return new InspectionResult(InspectionDecision.EndOperation, "Error");
				case ClientMessage.ReadAllEventsFilteredCompleted.ReadAllFilteredResult.AccessDenied:
					Fail(new AccessDeniedException("Read access denied for $all."));
					return new InspectionResult(InspectionDecision.EndOperation, "AccessDenied");
				default:
					throw new Exception(string.Format("Unexpected ReadAllResult: {0}.", response.Result));
			}
		}

		protected override AllEventsSlice TransformResponse(ClientMessage.ReadAllEventsFilteredCompleted response) {
			return new AllEventsSlice(ReadDirection.Forward,
				new Position(response.CommitPosition, response.PreparePosition),
				new Position(response.NextCommitPosition, response.NextPreparePosition),
				response.Events, response.IsEndOfStream);
		}

		public override string ToString() {
			return string.Format("Position: {0}, MaxCount: {1}, ResolveLinkTos: {2}, RequireMaster: {3}",
				_position, _maxCount, _resolveLinkTos, _requireMaster);
		}
	}
}
