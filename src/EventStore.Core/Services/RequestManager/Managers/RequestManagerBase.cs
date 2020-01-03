using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Principal;
using System.Threading;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Commit;
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
		protected readonly long ExpectedVersion;

		protected OperationResult Result;
		protected long FirstEventNumber = -1;
		protected long LastEventNumber = -1;
		protected string FailureMessage = string.Empty;
		protected long FailureCurrentVersion = -1;
		protected long TransactionId;

		private bool _completeOnLogCommitted;
		private readonly ICommitSource _commitSource;
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
		private long _complete;

		private bool _commitRecieved;
		private int _prepareCount;

		protected DateTime NextTimeoutTime;
		private readonly TimeSpan _timeoutOffset = TimeSpan.FromMilliseconds(30);


		protected RequestManagerBase(
				IPublisher publisher,
				TimeSpan timeout,
				IEnvelope clientResponseEnvelope,
				Guid internalCorrId,
				Guid clientCorrId,
				long expectedVersion,
				ICommitSource commitSource,
				int prepareCount = 0,
				long transactionId = -1,
				bool waitForCommit = false,
				bool completeOnLogCommitted = false) {
			Ensure.NotEmptyGuid(internalCorrId, nameof(internalCorrId));
			Ensure.NotEmptyGuid(clientCorrId, nameof(clientCorrId));
			Ensure.NotNull(publisher, nameof(publisher));
			Ensure.NotNull(clientResponseEnvelope, nameof(clientResponseEnvelope));
			Ensure.NotNull(commitSource, nameof(commitSource));

			Publisher = publisher;
			Timeout = timeout;
			_clientResponseEnvelope = clientResponseEnvelope;
			InternalCorrId = internalCorrId;
			ClientCorrId = clientCorrId;
			WriteReplyEnvelope = new PublishEnvelope(Publisher);
			ExpectedVersion = expectedVersion;
			_commitSource = commitSource;
			_prepareCount = prepareCount;
			TransactionId = transactionId;
			_commitRecieved = !waitForCommit; //if not waiting for commit flag as true
			_allPreparesWritten = _prepareCount == 0; //if not waiting for prepares flag as true
			_completeOnLogCommitted = completeOnLogCommitted;
			if (prepareCount == 0 && waitForCommit == false) {
				//empty operation just return sucess
				var position = Math.Max(transactionId, 0);
				ReturnCommitAt(position, 0, 0);
			}
		}
		protected DateTime LiveUntil => NextTimeoutTime - _timeoutOffset;
		protected abstract Message AccessRequestMsg { get; }
		protected abstract Message WriteRequestMsg { get; }
		protected abstract Message ClientSuccessMsg { get; }
		protected abstract Message ClientFailMsg { get; }
		public void Start() {
			NextTimeoutTime = DateTime.UtcNow + Timeout;
			Publisher.Publish(AccessRequestMsg ?? WriteRequestMsg);
		}
		public void Handle(StorageMessage.CheckStreamAccessCompleted message) {
			if (Interlocked.Read(ref _complete) == 1) { return; }
			if (message.AccessResult.Granted) {
				Publisher.Publish(WriteRequestMsg);
			} else {
				CompleteFailedRequest(OperationResult.AccessDenied, "Access denied.");
			}
		}

		public void Handle(StorageMessage.PrepareAck message) {
			if (Interlocked.Read(ref _complete) == 1 || _allPreparesWritten) { return; }
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
			if (Interlocked.Read(ref _complete) == 1 || _commitRecieved) { return; }
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
		private bool _registered;
		protected virtual void AllEventsWritten() {
			var pos = _completeOnLogCommitted ? _commitSource.LogCommitPosition : _commitSource.CommitPosition;
			if (pos >= _lastEventPosition) {
				Committed();
			} else if (!_registered) {
				if (_completeOnLogCommitted) {
					_commitSource.NotifyLogCommitFor(_lastEventPosition, Committed);
				} else {
					_commitSource.NotifyCommitFor(_lastEventPosition, Committed);
				}
				_registered = true;
			}
		}
		protected virtual void Committed() {
			if (Interlocked.Read(ref _complete) == 1) { return; }
			Interlocked.Exchange(ref _complete, 1);
			Result = OperationResult.Success;
			_clientResponseEnvelope.ReplyWith(ClientSuccessMsg);
			Publisher.Publish(new StorageMessage.RequestCompleted(InternalCorrId, true));
		}
		public void Handle(StorageMessage.RequestManagerTimerTick message) {
			if (_allEventsWritten) { AllEventsWritten(); }
			if (Interlocked.Read(ref _complete) == 1 || message.UtcNow < NextTimeoutTime)
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
			if (Interlocked.Read(ref _complete) == 1 || _allEventsWritten) { return; }
			Log.Trace("IDEMPOTENT WRITE TO STREAM ClientCorrelationID {clientCorrelationId}, {message}.", ClientCorrId,
				message);
			ReturnCommitAt(message.LogPosition, message.FirstEventNumber, message.LastEventNumber);
		}
		protected virtual void ReturnCommitAt(long logPosition, long firstEvent, long lastEvent) {
			lock (_prepareLogPositions) {
				_prepareLogPositions.Clear();
				_prepareLogPositions.Add(logPosition);

				FirstEventNumber = firstEvent;
				LastEventNumber = lastEvent;
				CommitPosition = logPosition;
				Committed();
			}
		}

		private void CompleteFailedRequest(OperationResult result, string error, long currentVersion = -1) {
			if (Interlocked.Read(ref _complete) == 1) { return; }
			Debug.Assert(result != OperationResult.Success);
			Interlocked.Exchange(ref _complete, 1);
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
						if (Interlocked.Read(ref _complete) != 1) {
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
