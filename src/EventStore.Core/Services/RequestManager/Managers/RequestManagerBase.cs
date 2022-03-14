using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.TransactionLog.LogRecords;
using ILogger = Serilog.ILogger;

namespace EventStore.Core.Services.RequestManager.Managers {
	public abstract class RequestManagerBase :
		IHandle<StorageMessage.PrepareAck>,
		IHandle<StorageMessage.CommitIndexed>,
		IHandle<StorageMessage.InvalidTransaction>,
		IHandle<StorageMessage.StreamDeleted>,
		IHandle<StorageMessage.WrongExpectedVersion>,
		IHandle<StorageMessage.AlreadyCommitted>,
		IDisposable {
		public enum RequestState : int {
			Created,
			WaitingLocalCommit,
			CommitedLocal,
			WaitingClusterCommit,
			CommitedCluster,
			WaitingLocalIndex,
			IndexedLocal,
			CompletedFail,
			CompletedSuccess
		}
		private static readonly ILogger Log = Serilog.Log.ForContext<RequestManagerBase>();

		protected readonly IPublisher Publisher;
		public readonly long StartOffset;
		protected TimeSpan Timeout;
		//protected long Phase;

		protected readonly IEnvelope WriteReplyEnvelope;

		private readonly IEnvelope _clientResponseEnvelope;
		protected readonly Guid InternalCorrId;
		protected readonly Guid ClientCorrId;
		protected readonly long ExpectedVersion;

		public OperationResult Result { get; protected set; }
		protected long FirstEventNumber = -1;
		protected long LastEventNumber = -1;
		protected string FailureMessage = string.Empty;
		protected long FailureCurrentVersion = -1;
		protected long TransactionId;

		protected readonly CommitSource CommitSource;
		protected long LastEventPosition;
		protected bool Registered;
		protected long CommitPosition = -1;

		private readonly HashSet<long> _prepareLogPositions = new HashSet<long>();

		protected CancellationTokenSource RequestCanceled = new CancellationTokenSource();
		public RequestState State => CurrentState;
		protected volatile RequestState CurrentState;
		private readonly int _prepareCount;



		protected RequestManagerBase(
				IPublisher publisher,
				long startOffset,
				TimeSpan timeout,
				IEnvelope clientResponseEnvelope,
				Guid internalCorrId,
				Guid clientCorrId,
				long expectedVersion,
				CommitSource commitSource,
				long transactionId = -1,
				bool waitForCommit = false) {
			Ensure.NotEmptyGuid(internalCorrId, nameof(internalCorrId));
			Ensure.NotEmptyGuid(clientCorrId, nameof(clientCorrId));
			Ensure.NotNull(publisher, nameof(publisher));
			Ensure.NotNull(clientResponseEnvelope, nameof(clientResponseEnvelope));
			Ensure.NotNull(commitSource, nameof(commitSource));
			Ensure.Nonnegative(startOffset, nameof(startOffset));

			CurrentState = RequestState.Created;
			Publisher = publisher;
			StartOffset = startOffset;
			Timeout = timeout;
			_clientResponseEnvelope = clientResponseEnvelope;
			InternalCorrId = internalCorrId;
			ClientCorrId = clientCorrId;
			WriteReplyEnvelope = new PublishEnvelope(Publisher);
			ExpectedVersion = expectedVersion;
			CommitSource = commitSource;
			TransactionId = transactionId;



			//if (prepareCount == 0 && waitForCommit == false) {
			//	//empty operation just return success
			//	var position = Math.Max(transactionId, 0);
			//	ReturnCommitAt(position, 0, 0);
			//}
		}

		protected abstract Task WaitForLocalCommit();
		protected abstract Task WaitForClusterCommit();
		protected abstract Task WaitForLocalIndex();
		protected abstract Message WriteRequestMsg { get; }
		protected abstract Message ClientSuccessMsg { get; }
		protected abstract Message ClientFailMsg { get; }

		public void Start() {
			Publisher.Publish(WriteRequestMsg);
			Task.Run(() => {
				WaitForLocalCommit().ContinueWith((_) =>
				WaitForClusterCommit(), TaskContinuationOptions.OnlyOnRanToCompletion).ContinueWith((_) =>
				WaitForLocalIndex(), TaskContinuationOptions.OnlyOnRanToCompletion).ContinueWith((_) =>
				RequestCompleted(), TaskContinuationOptions.OnlyOnRanToCompletion);
			});
		}


		public abstract void Handle(StorageMessage.PrepareAck message);
		//{
		//	if (Interlocked.Read(ref _complete) == 1 || _allPreparesWritten) { return; }
		//	_phaseCancelTokenSource.Cancel();
		//	_phaseCancelTokenSource.Dispose();
		//	_phaseCancelTokenSource = new CancellationTokenSource(Timeout);

		//	if (message.Flags.HasAnyOf(PrepareFlags.TransactionBegin)) {
		//		TransactionId = message.LogPosition;
		//	}
		//	if (message.LogPosition > LastEventPosition) {
		//		LastEventPosition = message.LogPosition;
		//	}

		//	lock (_prepareLogPositions) {
		//		_prepareLogPositions.Add(message.LogPosition);
		//		_allPreparesWritten = _prepareLogPositions.Count == _prepareCount;
		//	}
		//	if (_allPreparesWritten) { AllPreparesWritten(); }
		//	_allEventsWritten = _commitReceived && _allPreparesWritten;
		//	if (_allEventsWritten) { AllEventsWritten(); }
		//	Task
		//		.Delay(Timeout, _phaseCancelTokenSource.Token)
		//		.ContinueWith((delay) => {
		//			if (delay.IsCanceled) { delay.Dispose(); }
		//			if (delay.IsCompleted) { CancelRequest(); }
		//		});
		//}
		public abstract void Handle(StorageMessage.CommitIndexed message);
		//	{
		//	if (Interlocked.Read(ref _complete) == 1 || _commitReceived) { return; }
		//	_phaseCancelTokenSource.Cancel();
		//	_phaseCancelTokenSource.Dispose();
		//	_phaseCancelTokenSource = new CancellationTokenSource(Timeout);

		//	_commitReceived = true;
		//	_allEventsWritten = _commitReceived && _allPreparesWritten;
		//	if (message.LogPosition > LastEventPosition) {
		//		LastEventPosition = message.LogPosition;
		//	}
		//	FirstEventNumber = message.FirstEventNumber;
		//	LastEventNumber = message.LastEventNumber;
		//	CommitPosition = message.LogPosition;
		//	if (_allEventsWritten) { AllEventsWritten(); }
		//	Task
		//		.Delay(Timeout, _phaseCancelTokenSource.Token)
		//		.ContinueWith((delay) => {
		//			if (delay.IsCanceled) { delay.Dispose(); }
		//			if (delay.IsCompleted) { CancelRequest(); }
		//		});
		//}
		protected virtual void AllPreparesWritten() { }

		protected virtual void AllEventsWritten() {
			if (!Registered) {
				//_phaseCancelTokenSource.Cancel();
				//_phaseCancelTokenSource.Dispose();
				//_phaseCancelTokenSource = new CancellationTokenSource(Timeout);

				var tokenSource = new CancellationTokenSource(Timeout);
				var cancellationToken = tokenSource.Token;
				try {
					CommitSource
						.WaitForIndexing(LastEventPosition, cancellationToken)
						.ContinueWith((_) => RequestCompleted());
				} catch {
					CancelRequest();
				} finally { tokenSource.Dispose(); }
				Registered = true;
			}
		}
		protected virtual void ReturnCommitAt(long logPosition, long firstEvent, long lastEvent) {
			lock (_prepareLogPositions) {
				_prepareLogPositions.Clear();
				_prepareLogPositions.Add(logPosition);

				FirstEventNumber = firstEvent;
				LastEventNumber = lastEvent;
				CommitPosition = logPosition;
				RequestCompleted();
			}
		}
		protected virtual void RequestCompleted() {
			if (CurrentState >= RequestState.CompletedFail) { return; }
			Interlocked.Exchange(ref CurrentState, (long)RequestState.CompletedSuccess);
			Result = OperationResult.Success;
			_clientResponseEnvelope.ReplyWith(ClientSuccessMsg);
			Publisher.Publish(new StorageMessage.RequestCompleted(InternalCorrId, true));
		}

		public void CancelRequest() {
			if (Interlocked.Read(ref _complete) == 1) { return; }
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
			if (Interlocked.Read(ref _complete) == 1 /*|| _allEventsWritten*/) { return; }
			Log.Debug("IDEMPOTENT WRITE TO STREAM ClientCorrelationID {clientCorrelationId}, {message}.", ClientCorrId,
				message);
			ReturnCommitAt(message.LogPosition, message.FirstEventNumber, message.LastEventNumber);
		}
		protected void CompleteFailedRequest(OperationResult result, string error, long currentVersion = -1) {
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
							//todo (clc) need a better Result here, but need to see if this will impact the client API
							var result = !_allPreparesWritten ? OperationResult.PrepareTimeout : OperationResult.CommitTimeout;
							var msg = "Request canceled by server, likely deposed leader";
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
