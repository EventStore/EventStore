using System;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Services.Storage.ReaderIndex;

namespace EventStore.Core.Services.RequestManager.Managers {
	public class DeleteStreamRequestManager : SinglePhaseRequestManagerBase,
		IHandle<ClientMessage.DeleteStream> {
		private string _eventStreamId;
		private long _expectedVersion;
		private bool _hardDelete;
		private ClientMessage.DeleteStreamCompleted _responseMsg;

		public DeleteStreamRequestManager(
			IPublisher publisher,
			TimeSpan timeout,
			bool betterOrdering)
			: base(publisher, timeout, betterOrdering) {
		}

		public void Handle(ClientMessage.DeleteStream request) {
			_eventStreamId = request.EventStreamId;
			_expectedVersion = request.ExpectedVersion;
			_hardDelete = request.HardDelete;
			Init(request.Envelope, request.InternalCorrId, request.CorrelationId, request.EventStreamId,
				request.User, StreamAccessType.Delete);
		}

		protected override void OnSecurityAccessGranted(Guid internalCorrId) {
			Publisher.Publish(
				new StorageMessage.WriteDelete(
					internalCorrId, PublishEnvelope, _eventStreamId, _expectedVersion, _hardDelete,
					liveUntil: NextTimeoutTime - TimeoutOffset));
		}

		protected override void SuccessLocalCommitted(long firstEventNumber, long lastEventNumber,
			long preparePosition, long commitPosition) {
			base.SuccessLocalCommitted(firstEventNumber, lastEventNumber, preparePosition, commitPosition);
			_responseMsg = new ClientMessage.DeleteStreamCompleted(ClientCorrId, OperationResult.Success, null,
				preparePosition, commitPosition);

		}

		protected override void SuccessClusterCommitted() {
			base.SuccessClusterCommitted();
			ResponseEnvelope.ReplyWith(_responseMsg);
		}

		protected override void CompleteFailedRequest(OperationResult result, string error, long currentVersion = -1) {
			base.CompleteFailedRequest(result, error, currentVersion);
			var responseMsg = new ClientMessage.DeleteStreamCompleted(ClientCorrId, result, error);
			ResponseEnvelope.ReplyWith(responseMsg);
		}
	}
}
