using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using EventStore.ClientAPI.Exceptions;
using EventStore.ClientAPI.Messages;
using EventStore.ClientAPI.SystemData;

namespace EventStore.ClientAPI.ClientOperations {
	internal class AppendToStreamOperation : OperationBase<WriteResult, ClientMessage.WriteEventsCompleted> {
		private readonly bool _requireMaster;
		private readonly string _stream;
		private readonly long _expectedVersion;
		private readonly IEnumerable<EventData> _events;

		private bool _wasCommitTimeout;

		public AppendToStreamOperation(ILogger log,
			TaskCompletionSource<WriteResult> source,
			bool requireMaster,
			string stream,
			long expectedVersion,
			IEnumerable<EventData> events,
			UserCredentials userCredentials)
			: base(log, source, TcpCommand.WriteEvents, TcpCommand.WriteEventsCompleted, userCredentials) {
			_requireMaster = requireMaster;
			_stream = stream;
			_expectedVersion = expectedVersion;
			_events = events;
		}

		protected override object CreateRequestDto() {
			var dtos = _events.Select(x =>
					new ClientMessage.NewEvent(x.EventId.ToByteArray(), x.Type, x.IsJson ? 1 : 0, 0, x.Data,
						x.Metadata))
				.ToArray();
			return new ClientMessage.WriteEvents(_stream, _expectedVersion, dtos, _requireMaster);
		}

		protected override InspectionResult InspectResponse(ClientMessage.WriteEventsCompleted response) {
			switch (response.Result) {
				case ClientMessage.OperationResult.Success:
					if (_wasCommitTimeout)
						Log.Debug("IDEMPOTENT WRITE SUCCEEDED FOR {0}.", this);
					Succeed();
					return new InspectionResult(InspectionDecision.EndOperation, "Success");
				case ClientMessage.OperationResult.PrepareTimeout:
					return new InspectionResult(InspectionDecision.Retry, "PrepareTimeout");
				case ClientMessage.OperationResult.ForwardTimeout:
					return new InspectionResult(InspectionDecision.Retry, "ForwardTimeout");
				case ClientMessage.OperationResult.CommitTimeout:
					_wasCommitTimeout = true;
					return new InspectionResult(InspectionDecision.Retry, "CommitTimeout");
				case ClientMessage.OperationResult.WrongExpectedVersion:
					var err = string.Format(
						"Append failed due to WrongExpectedVersion. Stream: {0}, Expected version: {1}, Current version: {2}",
						_stream, _expectedVersion, response.CurrentVersion);
					Fail(new WrongExpectedVersionException(err, _expectedVersion, response.CurrentVersion));
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

		protected override WriteResult TransformResponse(ClientMessage.WriteEventsCompleted response) {
			return new WriteResult(response.LastEventNumber,
				new Position(response.PreparePosition ?? -1, response.CommitPosition ?? -1));
		}

		public override string ToString() {
			return string.Format("Stream: {0}, ExpectedVersion: {1}", _stream, _expectedVersion);
		}
	}
}
