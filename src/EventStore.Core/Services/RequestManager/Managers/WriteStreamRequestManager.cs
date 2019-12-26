using System;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Services.Storage.ReaderIndex;

namespace EventStore.Core.Services.RequestManager.Managers {
	public class WriteStreamRequestManager : SinglePhaseRequestManagerBase,
		IHandle<ClientMessage.WriteEvents> {
		private ClientMessage.WriteEvents _request;
		private ClientMessage.WriteEventsCompleted _responseMsg;

		public WriteStreamRequestManager(
			IPublisher publisher,
			TimeSpan timeout,
			bool betterOrdering)
			: base(publisher,timeout, betterOrdering) {
		}

		public void Handle(ClientMessage.WriteEvents request) {
			_request = request;
			Init(request.Envelope, request.InternalCorrId, request.CorrelationId,
				request.EventStreamId, request.User, StreamAccessType.Write);
		}

		protected override void OnSecurityAccessGranted(Guid internalCorrId) {
			Publisher.Publish(
				new StorageMessage.WritePrepares(
					internalCorrId, PublishEnvelope, _request.EventStreamId, _request.ExpectedVersion, _request.Events,
					liveUntil: NextTimeoutTime - TimeoutOffset));
			_request = null;
		}
		
		protected override void SuccessLocalCommitted(long firstEventNumber, long lastEventNumber,
			long preparePosition, long commitPosition) {
			base.SuccessLocalCommitted(firstEventNumber, lastEventNumber, preparePosition, commitPosition);
			_responseMsg = new ClientMessage.WriteEventsCompleted(ClientCorrId, firstEventNumber,
				lastEventNumber, preparePosition, commitPosition);
		}

		protected override void SuccessClusterCommitted() {
			base.SuccessClusterCommitted();
			ResponseEnvelope.ReplyWith(_responseMsg);
		}
		protected override void CompleteFailedRequest(OperationResult result, string error, long currentVersion = -1) {
			base.CompleteFailedRequest(result, error, currentVersion);
			ResponseEnvelope.ReplyWith(
				new ClientMessage.WriteEventsCompleted(ClientCorrId, result, error, currentVersion));
		}
	}
}
