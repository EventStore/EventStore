using System;
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core.Messages {
	public static class CoreProjectionCheckpointWriterMessage {
		public sealed class CheckpointWritten : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			private readonly CheckpointTag _position;

			public CheckpointWritten(CheckpointTag position) {
				_position = position;
			}

			public CheckpointTag Position {
				get { return _position; }
			}
		}

		public sealed class RestartRequested : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public string Reason {
				get { return _reason; }
			}

			private readonly string _reason;

			public RestartRequested(string reason) {
				_reason = reason;
			}
		}
	}
}

public static class CoreProjectionProcessingMessage {
	public abstract class Message : EventStore.Core.Messaging.Message {
		private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

		public override int MsgTypeId {
			get { return TypeId; }
		}

		private readonly Guid _projectionId;

		protected Message(Guid projectionId) {
			_projectionId = projectionId;
		}

		public Guid ProjectionId {
			get { return _projectionId; }
		}
	}

	public class CheckpointLoaded : Message {
		private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

		public override int MsgTypeId {
			get { return TypeId; }
		}

		private readonly CheckpointTag _checkpointTag;
		private readonly string _checkpointData;
		private readonly long _checkpointEventNumber;

		public CheckpointLoaded(
			Guid projectionId, CheckpointTag checkpointTag, string checkpointData, long checkpointEventNumber)
			: base(projectionId) {
			_checkpointTag = checkpointTag;
			_checkpointData = checkpointData;
			_checkpointEventNumber = checkpointEventNumber;
		}

		public CheckpointTag CheckpointTag {
			get { return _checkpointTag; }
		}

		public string CheckpointData {
			get { return _checkpointData; }
		}

		public long CheckpointEventNumber {
			get { return _checkpointEventNumber; }
		}
	}

	public class PrerecordedEventsLoaded : Message {
		private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

		public override int MsgTypeId {
			get { return TypeId; }
		}

		private readonly CheckpointTag _checkpointTag;

		public PrerecordedEventsLoaded(Guid projectionId, CheckpointTag checkpointTag)
			: base(projectionId) {
			_checkpointTag = checkpointTag;
		}

		public CheckpointTag CheckpointTag {
			get { return _checkpointTag; }
		}
	}

	public class CheckpointCompleted : Message {
		private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

		public override int MsgTypeId {
			get { return TypeId; }
		}

		private readonly CheckpointTag _checkpointTag;

		public CheckpointCompleted(Guid projectionId, CheckpointTag checkpointTag)
			: base(projectionId) {
			_checkpointTag = checkpointTag;
		}

		public CheckpointTag CheckpointTag {
			get { return _checkpointTag; }
		}
	}

	public class RestartRequested : Message {
		private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

		public override int MsgTypeId {
			get { return TypeId; }
		}

		private readonly string _reason;

		public RestartRequested(Guid projectionId, string reason)
			: base(projectionId) {
			_reason = reason;
		}

		public string Reason {
			get { return _reason; }
		}
	}

	public class Failed : Message {
		private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

		public override int MsgTypeId {
			get { return TypeId; }
		}

		private readonly string _reason;

		public Failed(Guid projectionId, string reason)
			: base(projectionId) {
			_reason = reason;
		}

		public string Reason {
			get { return _reason; }
		}
	}

	public class ReadyForCheckpoint : EventStore.Core.Messaging.Message {
		private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

		public override int MsgTypeId {
			get { return TypeId; }
		}

		private readonly object _sender;

		public ReadyForCheckpoint(object sender) {
			_sender = sender;
		}

		public object Sender {
			get { return _sender; }
		}
	}

	public class EmittedStreamAwaiting : EventStore.Core.Messaging.Message {
		private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

		public override int MsgTypeId {
			get { return TypeId; }
		}

		private readonly IEnvelope _envelope;
		private readonly string _streamId;

		public EmittedStreamAwaiting(string streamId, IEnvelope envelope) {
			_envelope = envelope;
			_streamId = streamId;
		}

		public string StreamId {
			get { return _streamId; }
		}

		public IEnvelope Envelope {
			get { return _envelope; }
		}
	}

	public class EmittedStreamWriteCompleted : EventStore.Core.Messaging.Message {
		private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

		public override int MsgTypeId {
			get { return TypeId; }
		}

		private readonly string _streamId;

		public EmittedStreamWriteCompleted(string streamId) {
			_streamId = streamId;
		}

		public string StreamId {
			get { return _streamId; }
		}
	}
}
