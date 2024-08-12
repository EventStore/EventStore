using System;
using EventStore.Core.Util;
using EventStore.Projections.Core.Messages;
using Serilog;

namespace EventStore.Projections.Core.Services.Processing {
	public class CommittedEventWorkItem : WorkItem {
		private readonly EventReaderSubscriptionMessage.CommittedEventReceived _message;
		private string _partition;
		private readonly IEventProcessingProjectionPhase _projection;
		private readonly StatePartitionSelector _statePartitionSelector;
		private EventProcessedResult _eventProcessedResult;
		protected readonly RetryStrategy RetryStrategy;
		public override bool IsReady { get => RetryStrategy.IsReady(); }
		public override string Id { get => _message.CheckpointTag.ToString(); }
		
		public CommittedEventWorkItem(
			IEventProcessingProjectionPhase projection, EventReaderSubscriptionMessage.CommittedEventReceived message,
			StatePartitionSelector statePartitionSelector)
			: base(null) {
			_projection = projection;
			_statePartitionSelector = statePartitionSelector;
			_message = message;
			_requiresRunning = true;
			RetryStrategy = new DefaultRetryStrategy(TimeSpan.FromMilliseconds(100));
		}

		protected override void RecordEventOrder() {
			_projection.RecordEventOrder(_message.Data, _message.CheckpointTag, () => NextStage());
		}

		protected override void GetStatePartition() {
			_partition = _statePartitionSelector.GetStatePartition(_message);
			if (_partition == null)
				// skip processing of events not mapped to any partition
				NextStage();
			else
				NextStage(_partition);
		}

		protected override void Load(CheckpointTag checkpointTag) {
			if (_partition == null) {
				NextStage();
				return;
			}

			// we load partition state even if stopping etc.  should we skip?
			_projection.BeginGetPartitionStateAt(
				_partition, _message.CheckpointTag, LoadCompleted, lockLoaded: true);
		}

		private void LoadCompleted(PartitionState state) {
			NextStage();
		}

		protected override void ProcessEvent() {
			if (_partition == null) {
				NextStage();
				return;
			}

			try {
				var eventProcessedResult = _projection.ProcessCommittedEvent(_message, _partition);
				if (eventProcessedResult != null)
					SetEventProcessedResult(eventProcessedResult);
				NextStage();
				RetryStrategy.Success();
			} catch (Exception ex) {
				if (ex is TimeoutException) {
					if (RetryStrategy.Failed(ex, _ => _projection.Failed(_message, ex))) {
						//if retries exhausted, advance
						Log.Error($"Retries exhausted for task : {Id}");
						NextStage();
					} else {
						//don't advance stage for the task, when retryable
						_complete(_onStage, _lastStageCorrelationId);
					}
				} else {
					//don't need to retry
					RetryStrategy.Success();
					_projection.Failed(_message, ex);
					NextStage();
				}
			}
		}

		protected override void WriteOutput() {
			if (_partition == null) {
				NextStage();
				return;
			}

			_projection.FinalizeEventProcessing(_eventProcessedResult, _message.CheckpointTag, _message.Progress);
			NextStage();
		}

		private void SetEventProcessedResult(EventProcessedResult eventProcessedResult) {
			_eventProcessedResult = eventProcessedResult;
		}
	}
}
