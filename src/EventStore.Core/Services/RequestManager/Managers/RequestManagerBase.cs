using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.TransactionLog.LogRecords;

namespace EventStore.Core.Services.RequestManager.Managers {
	public abstract class RequestManagerBase :
		IHandle<StorageMessage.CheckStreamAccessCompleted>,
		IHandle<StorageMessage.PrepareAck>,
		IHandle<StorageMessage.CommitAck>,
		IHandle<StorageMessage.InvalidTransaction>,
		IHandle<StorageMessage.StreamDeleted>,
		IHandle<StorageMessage.WrongExpectedVersion>,
		IHandle<CommitMessage.LogCommittedTo>,
		IHandle<CommitMessage.CommittedTo>,
		IHandle<StorageMessage.AlreadyCommitted>,
		IHandle<StorageMessage.RequestManagerTimerTick>,
		IDisposable {

		private static readonly ILogger Log = LogManager.GetLoggerFor<RequestManagerBase>();

		protected readonly IPublisher Publisher;
		protected TimeSpan Timeout;
		protected readonly IEnvelope WriteReplyEnvelope;

		private readonly IEnvelope _clientResponseEnvelope;
		protected readonly Guid InternalCorrId;
		protected readonly Guid ClientCorrId;
		protected readonly string StreamId;
		protected readonly IPrincipal User;
		protected readonly long ExpectedVersion;
		protected OperationResult Result;
		protected long FirstEventNumber = -1;
		protected long LastEventNumber = -1;
		protected string FailureMessage = string.Empty;
		protected long FailureCurrentVersion = -1;
		protected long TransactionId;

		private bool _betterOrdering;
		private bool _completeOnLogCommitted;
		private long _committedPosition;
		private long _lastEventPosition;
		protected long CommitPosition = -1;
		protected long FirstPrepare {
			get {
				long logPosition = -1;
				if (_prepareCount == 0) {
					logPosition = CommitPosition; //no prepares so use commit
				} else {
					lock (_prepareLogPositions) {
						if (_prepareLogPositions.Count > 0) {
							// get the lowest prepare log position for this request
							// not guaranteed to be the TransactionId in multi-request writes
							var positions = _prepareLogPositions.ToArray();
							Array.Sort(positions);
							logPosition = positions[positions.Length - 1];
						}
					}
				}
				return logPosition;
			}
		}
		private HashSet<long> _prepareLogPositions = new HashSet<long>();

		private bool _allEventsWritten;
		private bool _allPreparesWritten;
		private bool _complete;

		private bool _commitRecieved;
		private int _prepareCount;
		private bool _authenicate;

		protected DateTime NextTimeoutTime;
		private readonly TimeSpan _timeoutOffset = TimeSpan.FromMilliseconds(30);


		protected RequestManagerBase(
				IPublisher publisher,
				TimeSpan timeout,
				IEnvelope clientResponseEnvelope,
				Guid internalCorrId,
				Guid clientCorrId,
				string streamId,
				bool betterOrdering,
				long expectedVersion,
				IPrincipal user,
				int prepareCount = 0,
				long transactionId = -1,
				bool waitForCommit = false,
				bool authenticate = true,
				bool completeOnLogCommitted = false,
				long currentLogPosition = 0) {
			Ensure.NotEmptyGuid(internalCorrId, nameof(internalCorrId));
			Ensure.NotEmptyGuid(clientCorrId, nameof(clientCorrId));
			Ensure.NotNull(publisher, nameof(publisher));
			Ensure.NotNull(clientResponseEnvelope, nameof(clientResponseEnvelope));
			if (prepareCount == 0 && waitForCommit == false) {
				throw new InvalidOperationException($"{((dynamic)this).GetType().Name} implementing {nameof(RequestManagerBase)} cannot wait on no prepares and no commit!");
			}
			Publisher = publisher;
			Timeout = timeout;
			_clientResponseEnvelope = clientResponseEnvelope;
			InternalCorrId = internalCorrId;
			ClientCorrId = clientCorrId;
			WriteReplyEnvelope = new PublishEnvelope(Publisher);
			StreamId = streamId;
			_betterOrdering = betterOrdering;
			User = user;
			ExpectedVersion = expectedVersion;
			_prepareCount = prepareCount;
			TransactionId = transactionId;
			_commitRecieved = !waitForCommit; //if not waiting for commit flag as true
			_allPreparesWritten = _prepareCount == 0; //if not waiting for prepares flag as true
			_authenicate = authenticate;
			_completeOnLogCommitted = completeOnLogCommitted;
			_committedPosition = currentLogPosition;
		}
		protected DateTime LiveUntil => NextTimeoutTime - _timeoutOffset;
		public abstract Message WriteRequestMsg { get; }
		protected abstract Message ClientSuccessMsg { get; }
		protected abstract Message ClientFailMsg { get; }
		public void Start() {
			NextTimeoutTime = DateTime.UtcNow + Timeout;
			if (_authenicate) {
				long? transactionId = TransactionId == -1 ? (long?)null : TransactionId;
				Publisher.Publish(new StorageMessage.CheckStreamAccess(
						WriteReplyEnvelope, InternalCorrId, StreamId, transactionId, StreamAccessType.Write, User, _betterOrdering));
			} else {
				Publisher.Publish(WriteRequestMsg);
			}
		}
		public void Handle(StorageMessage.CheckStreamAccessCompleted message) {
			if (_complete) { return; }
			if (message.AccessResult.Granted) {
				Publisher.Publish(WriteRequestMsg);
			} else {
				CompleteFailedRequest(OperationResult.AccessDenied, "Access denied.");
			}
		}

		public void Handle(StorageMessage.PrepareAck message) {
			if (_complete || _allPreparesWritten) { return; }
			NextTimeoutTime = DateTime.UtcNow + Timeout;
			if (message.Flags.HasAnyOf(PrepareFlags.TransactionBegin)) {
				TransactionId = message.LogPosition;
			}
			if (message.LogPosition > _lastEventPosition) {
				_lastEventPosition = message.LogPosition;
			}

			lock (_prepareLogPositions) {
				_prepareLogPositions.Add(message.LogPosition);
				_allPreparesWritten = _prepareLogPositions.Count == _prepareCount;
			}
			if (_allPreparesWritten) { AllPreparesWritten(); }
			_allEventsWritten = _commitRecieved && _allPreparesWritten;
			if (_allEventsWritten) { AllEventsWritten(); }
		}
		public void Handle(StorageMessage.CommitAck message) {
			if (_complete || _commitRecieved) { return; }
			NextTimeoutTime = DateTime.UtcNow + Timeout;
			_commitRecieved = true;
			_allEventsWritten = _commitRecieved && _allPreparesWritten;
			if (message.LogPosition > _lastEventPosition) {
				_lastEventPosition = message.LogPosition;
			}
			FirstEventNumber = message.FirstEventNumber;
			LastEventNumber = message.LastEventNumber;
			CommitPosition = message.LogPosition;
			if (_allEventsWritten) { AllEventsWritten(); }
		}
		protected virtual void AllPreparesWritten() { }
		protected virtual void AllEventsWritten() {
			if (_committedPosition >= _lastEventPosition) {
				Committed();
			}
		}
		public void Handle(CommitMessage.CommittedTo message) {
			if (_completeOnLogCommitted) { return; }
			CommitPositionUpdated(message.LogPosition);
		}
		public void Handle(CommitMessage.LogCommittedTo message) {
			if (!_completeOnLogCommitted) { return; }
			CommitPositionUpdated(message.LogPosition);
		}
		private void CommitPositionUpdated(long logPosition) {
			if (_complete) { return; }
			if (logPosition > _committedPosition) {
				_committedPosition = logPosition;
			}
			if (_allEventsWritten && _committedPosition >= _lastEventPosition) {
				Committed();
			}
		}
		protected virtual void Committed() {
			if (_complete) { return; }
			_complete = true;
			Result = OperationResult.Success;
			_clientResponseEnvelope.ReplyWith(ClientSuccessMsg);
			Publisher.Publish(new StorageMessage.RequestCompleted(InternalCorrId, true));
		}
		public void Handle(StorageMessage.RequestManagerTimerTick message) {
			if (_complete || message.UtcNow < NextTimeoutTime)
				return;
			var result = !_allPreparesWritten ? OperationResult.PrepareTimeout : OperationResult.CommitTimeout;
			var msg = !_allPreparesWritten ? "Prepare phase timeout." : "Commit phase timeout.";
			CompleteFailedRequest(result, msg);
		}
		public void Handle(StorageMessage.InvalidTransaction message) {
			CompleteFailedRequest(OperationResult.InvalidTransaction, "Invalid transaction.");
		}
		public void Handle(StorageMessage.WrongExpectedVersion message) {
			FailureCurrentVersion = message.CurrentVersion;
			CompleteFailedRequest(OperationResult.WrongExpectedVersion, "Wrong expected version.", message.CurrentVersion);
		}
		public void Handle(StorageMessage.StreamDeleted message) {
			CompleteFailedRequest(OperationResult.StreamDeleted, "Stream is deleted.");
		}
		public void Handle(StorageMessage.AlreadyCommitted message) {
			if (_complete || _allEventsWritten) { return; }
			Log.Trace("IDEMPOTENT WRITE TO STREAM ClientCorrelationID {clientCorrelationId}, {message}.", ClientCorrId,
				message);

			lock (_prepareLogPositions) {
				_prepareLogPositions.Clear();
				_prepareLogPositions.Add(message.LogPosition);

				FirstEventNumber = message.FirstEventNumber;
				LastEventNumber = message.LastEventNumber;
				CommitPosition = message.LogPosition;
				Committed();
			}
		}

		private void CompleteFailedRequest(OperationResult result, string error, long currentVersion = -1) {
			if (_complete) { return; }
			Debug.Assert(result != OperationResult.Success);
			_complete = true;
			Result = result;
			FailureMessage = error;
			Publisher.Publish(new StorageMessage.RequestCompleted(InternalCorrId, false, currentVersion));
			_clientResponseEnvelope.ReplyWith(ClientFailMsg);
		}

		#region IDisposable Support
		private bool _disposed; // To detect redundant calls

		protected virtual void Dispose(bool disposing) {
			if (!_disposed) {
				if (disposing) {
					try {
						if (!_complete) {
							//todo-clc: need a better Result here, but need to see if this will impact the client API
							var result = !_allPreparesWritten ? OperationResult.PrepareTimeout : OperationResult.CommitTimeout;
							var msg = "Request canceled by server, likely deposed master";
							CompleteFailedRequest(result, msg);
						}
					} catch { /*don't throw in disposed*/}

					_disposed = true;
				}
			}
		}
		public void Dispose() {
			Dispose(true);
		}
		#endregion
	}
}
