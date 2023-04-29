using System;
using System.Collections.Generic;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core.Tests.Services;

public class FallibleCommittedWorkItemTask : CommittedEventWorkItem {
	private readonly Action<int> _handleRequestsForStage;
	private int _startedOnStage;
	private bool _isTaskReady = true;
	private readonly TestProjectionProcessingPhase _projection;
	public override bool IsReady { get => _isTaskReady; }

	public FallibleCommittedWorkItemTask(TestProjectionProcessingPhase projection, EventReaderSubscriptionMessage.CommittedEventReceived message, TestStatePartitionSelector statePartitionSelector, Action<int> handleRequestsForStage) : base(projection, message, statePartitionSelector) {
		_startedOnStage = -1;
		_handleRequestsForStage = handleRequestsForStage;
		_projection = projection;
		SetCheckpointTag(CheckpointTag.Empty);
	}

	public bool DidProjectionFail() {
		return _projection._didFail;
	}

	public bool StartedOn(int onStage) {
		return _startedOnStage >= onStage;
	}

	public override void Process(int onStage, Action<int, object> successfulProcessing) {
		_startedOnStage = onStage;
		base.Process(onStage, successfulProcessing);
	}

	protected override void CompleteItem() {
		_handleRequestsForStage(5);
		NextStage();
	}

	public void SetTaskReady(bool isReady = true) {
		_isTaskReady = isReady;
	}

	public int GetNumRetryableFailures() {
		return RetryStrategy.FailureCnt;
	}

	public class Failure {
		public int NumFailures { get; set; }
		public Exception Ex { get; }

		public Failure(int numFailures, Exception ex) {
			NumFailures = numFailures;
			Ex = ex;
		}
	}
	
	public class TestProjectionProcessingPhase : IEventProcessingProjectionPhase {
		internal bool _didFail = false;
		private readonly Action<int> _handleRequestsForStage;

		public TestProjectionProcessingPhase(Action<int> handleRequestsForStage) {
			_handleRequestsForStage = handleRequestsForStage;
		}

		public CheckpointTag LastProcessedEventPosition => default;

		public void BeginGetPartitionStateAt(string statePartition, CheckpointTag at, Action<PartitionState> loadCompleted, bool lockLoaded) {
			_handleRequestsForStage(2);
			loadCompleted(default);
		}

		public void UnlockAndForgetBefore(CheckpointTag checkpointTag) {
			//no-op
		}
		
		public EventProcessedResult ProcessCommittedEvent(EventReaderSubscriptionMessage.CommittedEventReceived message, string partition) {
			_handleRequestsForStage(3);
			return null;
		}

		public void FinalizeEventProcessing(EventProcessedResult result, CheckpointTag eventCheckpointTag, float progress) {
			_handleRequestsForStage(4);
		}

		public void RecordEventOrder(ResolvedEvent resolvedEvent, CheckpointTag orderCheckpointTag, Action completed) {
			_handleRequestsForStage(0);
			completed();
		}

		public void EmitEofResult(string partition, string resultBody, CheckpointTag causedBy, Guid causedByGuid,
			string correlationId) {
			//no-op
		}

		public EventProcessedResult ProcessPartitionDeleted(string partition, CheckpointTag deletedPosition) {
			return default;
		}

		public void Failed(EventReaderSubscriptionMessage.CommittedEventReceived message, Exception ex) {
			_didFail = true;
		}
	}
	
	public class TestStatePartitionSelector : StatePartitionSelector {
		private readonly Action<int> _handleRequestsForStage;

		public TestStatePartitionSelector(Action<int> handleRequestsForStage) {
			_handleRequestsForStage = handleRequestsForStage;
		}
		public override string GetStatePartition(EventReaderSubscriptionMessage.CommittedEventReceived @event) {
			_handleRequestsForStage(1);
			return Guid.NewGuid().ToString();
		}

		public override bool EventReaderBasePartitionDeletedIsSupported() => false;
		}
}
