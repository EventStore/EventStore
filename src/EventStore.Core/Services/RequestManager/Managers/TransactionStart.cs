using System;
using System.Threading;
using System.Threading.Tasks;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;

namespace EventStore.Core.Services.RequestManager.Managers {
	public class TransactionStart : RequestManagerBase {
		private readonly string _streamId;
		private TaskCompletionSource _stageTask;
		private CancellationTokenSource _stageTimeout;
		public TransactionStart(
					IPublisher publisher,
					long startOffset,
					TimeSpan timeout,
					IEnvelope clientResponseEnvelope,
					Guid internalCorrId,
					Guid clientCorrId,
					string streamId,
					long expectedVersion,
					CommitSource commitSource)
			: base(
					 publisher,
					 startOffset,
					 timeout,
					 clientResponseEnvelope,
					 internalCorrId,
					 clientCorrId,
					 expectedVersion,
					 commitSource,
					 prepareCount: 1) {
			_streamId = streamId;
			Result = OperationResult.PrepareTimeout; // we need an unknown here			
		}


		protected override Task WaitForLocalCommit() {
			if (CurrentState >= RequestState.WaitingLocalCommit) { return Task.CompletedTask; }
			CurrentState = RequestState.WaitingLocalCommit;
			_stageTask = new TaskCompletionSource();
			_stageTimeout = new CancellationTokenSource();
			Task.Delay(Timeout, _stageTimeout.Token)
				.ContinueWith((delay) => {
					if (delay.IsCanceled) { delay.Dispose(); }
					if (delay.IsCompleted) {
						_stageTask.TrySetCanceled();
						CompleteFailedRequest(OperationResult.PrepareTimeout, "Prepare phase timeout.");
					}
				});
			return _stageTask.Task;
		}

		protected override Task WaitForClusterCommit() {
			if (CurrentState >= RequestState.WaitingClusterCommit) { return Task.CompletedTask; }
			CurrentState = RequestState.WaitingClusterCommit;
			_stageTimeout = new CancellationTokenSource();
			Task.Delay(Timeout, _stageTimeout.Token)
				.ContinueWith((delay) => {
					if (delay.IsCanceled) { delay.Dispose(); }
					if (delay.IsCompleted) {
						_stageTask.TrySetCanceled();
						CompleteFailedRequest(OperationResult.PrepareTimeout, "Prepare phase timeout.");
					}
				});
			//todo: double check the edge cases for the correct token for timeout here
			return CommitSource
					.WaitForReplication(LastEventPosition, _stageTimeout.Token);
		}

		protected override Task WaitForLocalIndex() {
			var task = Task.CompletedTask;
			return task;
		}

		public override void Handle(StorageMessage.PrepareAck message) {
			TransactionId = message.LogPosition;
			LastEventPosition = message.LogPosition;
			CurrentState = RequestState.CommitedLocal;
			_stageTimeout?.Cancel();
			_stageTask?.SetResult();
		}
		public override void Handle(StorageMessage.CommitIndexed message) { return; }
		protected override Message WriteRequestMsg =>
			new StorageMessage.WriteTransactionStart(
					InternalCorrId,
					WriteReplyEnvelope,
					_streamId,
					ExpectedVersion,
					DateTime.UtcNow + Timeout);

		protected override Message ClientSuccessMsg =>
			 new ClientMessage.TransactionStartCompleted(
						ClientCorrId,
						TransactionId,
						OperationResult.Success,
						null);

		protected override Message ClientFailMsg =>
			 new ClientMessage.TransactionStartCompleted(
						ClientCorrId,
						TransactionId,
						Result,
						FailureMessage);

	}
}
