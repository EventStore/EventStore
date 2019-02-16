using System;
using System.Diagnostics;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Services.RequestManager.Managers {
	public class SingleAckRequestManager : IRequestManager,
		IHandle<ClientMessage.TransactionStart>,
		IHandle<ClientMessage.TransactionWrite>,
		IHandle<StorageMessage.CheckStreamAccessCompleted>,
		IHandle<StorageMessage.PrepareAck>,
		IHandle<StorageMessage.WrongExpectedVersion>,
		IHandle<StorageMessage.InvalidTransaction>,
		IHandle<StorageMessage.StreamDeleted>,
		IHandle<StorageMessage.RequestManagerTimerTick> {
		private readonly IPublisher _bus;
		private readonly TimeSpan _prepareTimeout;
		private readonly IEnvelope _publishEnvelope;

		private IEnvelope _responseEnvelope;
		private Guid _internalCorrId;
		private Guid _clientCorrId;

		private long _transactionId = -1;

		private bool _completed;
		private bool _initialized;
		private DateTime _nextTimeoutTime;
		private readonly bool _betterOrdering;
		private RequestType _requestType;
		private ClientMessage.TransactionStart _request;

		public SingleAckRequestManager(IPublisher bus, TimeSpan prepareTimeout, bool betterOrdering) {
			Ensure.NotNull(bus, "bus");

			_bus = bus;
			_prepareTimeout = prepareTimeout;
			_publishEnvelope = new PublishEnvelope(_bus);
			_betterOrdering = betterOrdering;
		}

		public void Handle(ClientMessage.TransactionStart request) {
			if (_initialized)
				throw new InvalidOperationException();

			_initialized = true;
			_requestType = RequestType.TransactionStart;
			_responseEnvelope = request.Envelope;
			_internalCorrId = request.InternalCorrId;
			_clientCorrId = request.CorrelationId;

			_transactionId = -1; // not known yet

			_request = request;
			_bus.Publish(new StorageMessage.CheckStreamAccess(
				_publishEnvelope, _internalCorrId, request.EventStreamId, null, StreamAccessType.Write, request.User,
				_betterOrdering));

			_nextTimeoutTime = DateTime.UtcNow + _prepareTimeout;
		}

		public void Handle(StorageMessage.CheckStreamAccessCompleted message) {
			if (_requestType != RequestType.TransactionStart || _request == null)
				throw new Exception(string.Format(
					"TransactionStart request manager invariant violation: reqType: {0}, req: {1}.", _requestType,
					_request));

			if (message.AccessResult.Granted) {
				_bus.Publish(new StorageMessage.WriteTransactionStart(
					_internalCorrId, _publishEnvelope, _request.EventStreamId, _request.ExpectedVersion,
					liveUntil: _nextTimeoutTime - TwoPhaseRequestManagerBase.TimeoutOffset));
				_request = null;
			} else {
				CompleteFailedRequest(OperationResult.AccessDenied, "Access denied.");
			}
		}

		public void Handle(ClientMessage.TransactionWrite request) {
			if (_initialized)
				throw new InvalidOperationException();

			_initialized = true;
			_requestType = RequestType.TransactionWrite;
			_responseEnvelope = request.Envelope;
			_internalCorrId = request.InternalCorrId;
			_clientCorrId = request.CorrelationId;

			_transactionId = request.TransactionId;

			_bus.Publish(new StorageMessage.WriteTransactionData(_internalCorrId, _publishEnvelope, _transactionId,
				request.Events));
			CompleteSuccessRequest();
		}

		public void Handle(StorageMessage.PrepareAck message) {
			if (_completed)
				return;
			if (message.Flags.HasNoneOf(PrepareFlags.TransactionBegin))
				throw new Exception(string.Format(
					"Unexpected PrepareAck with flags [{0}] arrived (LogPosition: {1}, InternalCorrId: {2:B}, ClientCorrId: {3:B}).",
					message.Flags, message.LogPosition, message.CorrelationId, _clientCorrId));
			_transactionId = message.LogPosition;
			CompleteSuccessRequest();
		}

		public void Handle(StorageMessage.WrongExpectedVersion message) {
			CompleteFailedRequest(OperationResult.WrongExpectedVersion, "Wrong expected version.");
		}

		public void Handle(StorageMessage.InvalidTransaction message) {
			CompleteFailedRequest(OperationResult.InvalidTransaction, "Invalid transaction.");
		}

		public void Handle(StorageMessage.StreamDeleted message) {
			CompleteFailedRequest(OperationResult.StreamDeleted, "Stream is deleted.");
		}

		public void Handle(StorageMessage.RequestManagerTimerTick message) {
			if (_completed || message.UtcNow < _nextTimeoutTime)
				return;

			CompleteFailedRequest(OperationResult.PrepareTimeout, "Prepare phase timeout.");
		}

		private void CompleteSuccessRequest() {
			_completed = true;
			Message responseMsg;
			switch (_requestType) {
				case RequestType.TransactionStart:
					responseMsg = new ClientMessage.TransactionStartCompleted(_clientCorrId, _transactionId,
						OperationResult.Success, null);
					break;
				case RequestType.TransactionWrite:
					responseMsg = new ClientMessage.TransactionWriteCompleted(_clientCorrId, _transactionId,
						OperationResult.Success, null);
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}

			_responseEnvelope.ReplyWith(responseMsg);
			_bus.Publish(new StorageMessage.RequestCompleted(_internalCorrId, true));
		}

		private void CompleteFailedRequest(OperationResult result, string error) {
			Debug.Assert(result != OperationResult.Success);

			_completed = true;
			Message responseMsg;
			switch (_requestType) {
				case RequestType.TransactionStart:
					responseMsg =
						new ClientMessage.TransactionStartCompleted(_clientCorrId, _transactionId, result, error);
					break;
				case RequestType.TransactionWrite:
					// Should never happen, only possibly under very heavy load...
					responseMsg =
						new ClientMessage.TransactionWriteCompleted(_clientCorrId, _transactionId, result, error);
					break;
				default:
					throw new ArgumentOutOfRangeException();
			}

			_responseEnvelope.ReplyWith(responseMsg);
			_bus.Publish(new StorageMessage.RequestCompleted(_internalCorrId, false));
		}

		private enum RequestType {
			TransactionStart,
			TransactionWrite
		}
	}
}
