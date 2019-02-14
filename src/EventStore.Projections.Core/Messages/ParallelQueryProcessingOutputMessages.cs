using System;
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core.Messages {
	namespace ParallelQueryProcessingMessages {
		public abstract class PartitionProcessingResultOutputBase : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			private readonly Guid _workerId;
			private readonly Guid _masterProjectionId;
			private readonly Guid _subscriptionId;
			private readonly string _partition;

			protected PartitionProcessingResultOutputBase(
				Guid workerId,
				Guid masterProjectionId,
				Guid subscriptionId,
				string partition) {
				_workerId = workerId;
				_masterProjectionId = masterProjectionId;
				_subscriptionId = subscriptionId;
				_partition = partition;
			}

			public string Partition {
				get { return _partition; }
			}

			public Guid SubscriptionId {
				get { return _subscriptionId; }
			}

			public Guid WorkerId {
				get { return _workerId; }
			}

			public Guid MasterProjectionId {
				get { return _masterProjectionId; }
			}

			public abstract Message AsInput();
		}

		public sealed class PartitionProcessingResultOutput : PartitionProcessingResultOutputBase {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public override Message AsInput() {
				return new PartitionProcessingResult(
					WorkerId,
					MasterProjectionId,
					SubscriptionId,
					Partition,
					CausedByGuid,
					Position,
					Result);
			}

			private readonly string _result;
			private readonly Guid _causedByGuid;
			private readonly CheckpointTag _position;

			public PartitionProcessingResultOutput(
				Guid workerId,
				Guid masterProjectionId,
				Guid subscriptionId,
				string partition,
				Guid causedByGuid,
				CheckpointTag position,
				string result)
				: base(workerId, masterProjectionId, subscriptionId, partition) {
				_causedByGuid = causedByGuid;
				_position = position;
				_result = result;
			}

			public string Result {
				get { return _result; }
			}

			public Guid CausedByGuid {
				get { return _causedByGuid; }
			}

			public CheckpointTag Position {
				get { return _position; }
			}
		}

		public sealed class PartitionMeasuredOutput : PartitionProcessingResultOutputBase {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public override Message AsInput() {
				return new PartitionMeasured(WorkerId, MasterProjectionId, SubscriptionId, Partition, Size);
			}

			private readonly long _size;

			public PartitionMeasuredOutput(
				Guid workerId,
				Guid masterProjectionId,
				Guid subscriptionId,
				string partition,
				long size)
				: base(workerId, masterProjectionId, subscriptionId, partition) {
				_size = size;
			}


			public long Size {
				get { return _size; }
			}
		}

		public sealed class PartitionProcessingProgressOutput : PartitionProcessingResultOutputBase {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public override Message AsInput() {
				return new PartitionProcessingProgress(WorkerId, MasterProjectionId, SubscriptionId, Progress);
			}

			private readonly float _progress;

			public PartitionProcessingProgressOutput(
				Guid workerId,
				Guid masterProjectionId,
				Guid subscriptionId,
				float progress)
				: base(workerId, masterProjectionId, subscriptionId, null) {
				_progress = progress;
			}

			public float Progress {
				get { return _progress; }
			}
		}
	}
}
