using System;
using System.Diagnostics;
using System.Security.Principal;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Services.RequestManager.Managers {
	public abstract class SinglePhaseRequestManagerBase : IRequestManager,
		IHandle<StorageMessage.CheckStreamAccessCompleted>,
		IHandle<StorageMessage.AlreadyCommitted>,
		IHandle<StorageMessage.CommitAck>,
		IHandle<StorageMessage.WrongExpectedVersion>,
		IHandle<StorageMessage.StreamDeleted>,
		IHandle<StorageMessage.RequestManagerTimerTick> {
		internal static readonly TimeSpan TimeoutOffset = TimeSpan.FromMilliseconds(30);
		private static readonly ILogger Log = LogManager.GetLoggerFor<TwoPhaseRequestManagerBase>();

		protected readonly IPublisher Publisher;
		protected readonly IEnvelope PublishEnvelope;

		protected IEnvelope ResponseEnvelope {
			get { return _responseEnvelope; }
		}

		protected Guid ClientCorrId {
			get { return _clientCorrId; }
		}

		protected DateTime NextTimeoutTime {
			get { return _nextTimeoutTime; }
		}

		protected readonly TimeSpan Timeout;
		private IEnvelope _responseEnvelope;
		private Guid _internalCorrId;
		private Guid _clientCorrId;

		private DateTime _nextTimeoutTime;

		private bool _localCommited;
		private bool _completed;
		private bool _initialized;
		private bool _betterOrdering;
		private long _transactionCommitPosition;
		private long _systemCommittedPosition;

		protected SinglePhaseRequestManagerBase(
			IPublisher publisher,
			TimeSpan timeout,
			bool betterOrdering) {
			Ensure.NotNull(publisher, "publisher");

			Publisher = publisher;
			PublishEnvelope = new PublishEnvelope(publisher);

			Timeout = timeout;
			_betterOrdering = betterOrdering;

			_transactionCommitPosition = long.MaxValue;
			_systemCommittedPosition = long.MinValue;
		}

		protected abstract void OnSecurityAccessGranted(Guid internalCorrId);

		protected void Init(IEnvelope responseEnvelope, Guid internalCorrId, Guid clientCorrId,
			string eventStreamId, IPrincipal user, StreamAccessType accessType) {
			if (_initialized)
				throw new InvalidOperationException();

			_initialized = true;

			_responseEnvelope = responseEnvelope;
			_internalCorrId = internalCorrId;
			_clientCorrId = clientCorrId;

			_nextTimeoutTime = DateTime.UtcNow + Timeout;
			Publisher.Publish(new StorageMessage.CheckStreamAccess(
				PublishEnvelope, internalCorrId, eventStreamId, null, accessType, user, _betterOrdering));
		}

		public void Handle(StorageMessage.CheckStreamAccessCompleted message) {
			if (message.AccessResult.Granted)
				OnSecurityAccessGranted(_internalCorrId);
			else
				CompleteFailedRequest(OperationResult.AccessDenied, "Access denied.");
		}

		public void Handle(StorageMessage.WrongExpectedVersion message) {
			if (_completed)
				return;

			CompleteFailedRequest(OperationResult.WrongExpectedVersion, "Wrong expected version.",
				message.CurrentVersion);
		}

		public void Handle(StorageMessage.StreamDeleted message) {
			if (_completed)
				return;

			CompleteFailedRequest(OperationResult.StreamDeleted, "Stream is deleted.");
		}

		public void Handle(StorageMessage.RequestManagerTimerTick message) {
			if (_completed || message.UtcNow < _nextTimeoutTime)
				return;
			CompleteFailedRequest(OperationResult.CommitTimeout, "Commit phase timeout.");
		}

		public void Handle(StorageMessage.AlreadyCommitted message) {
			if (_completed || _localCommited) { return; }
			Log.Trace("IDEMPOTENT WRITE TO STREAM ClientCorrelationID {clientCorrelationId}, {message}.", _clientCorrId,
				message);
			_transactionCommitPosition = message.LogPosition;
			SuccessLocalCommitted(message.FirstEventNumber, message.LastEventNumber, message.LogPosition, message.LogPosition);
			if (_systemCommittedPosition >= _transactionCommitPosition) { SuccessClusterCommitted(); }
		}

		public void Handle(StorageMessage.CommitAck message) {
			if (_completed) { return; }

			_transactionCommitPosition = message.LogPosition;
			SuccessLocalCommitted(message.FirstEventNumber, message.LastEventNumber, message.LogPosition,
				message.LogPosition);
			if (_systemCommittedPosition >= _transactionCommitPosition) { SuccessClusterCommitted(); }
		}

		public void Handle(CommitMessage.CommittedTo message) {
			if (_completed) { return; }
			_systemCommittedPosition = message.LogPosition;
			if (_systemCommittedPosition >= _transactionCommitPosition) { SuccessClusterCommitted(); }
		}

		protected virtual void SuccessLocalCommitted(long firstEventNumber, long lastEventNumber, long preparePosition,
			long commitPosition) {
			_localCommited = true;
		}

		protected virtual void SuccessClusterCommitted() {
			if (_completed) { return; }
			_completed = true;
			Publisher.Publish(new StorageMessage.RequestCompleted(_internalCorrId, true));
		}

		protected virtual void CompleteFailedRequest(OperationResult result, string error, long currentVersion = -1) {
			Debug.Assert(result != OperationResult.Success);
			_completed = true;
			Publisher.Publish(new StorageMessage.RequestCompleted(_internalCorrId, false, currentVersion));
		}
	}
}
