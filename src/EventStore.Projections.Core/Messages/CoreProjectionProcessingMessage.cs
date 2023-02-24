using System;
using EventStore.Core.Messaging;
using EventStore.Projections.Core.Messages;
using EventStore.Projections.Core.Services.Processing;

namespace EventStore.Projections.Core.Messages {
	public static partial class CoreProjectionCheckpointWriterMessage {
		[DerivedMessage(ProjectionMessage.CoreProcessing)]
		public sealed partial class CheckpointWritten : Message {
			private readonly CheckpointTag _position;

			public CheckpointWritten(CheckpointTag position) {
				_position = position;
			}

			public CheckpointTag Position {
				get { return _position; }
			}
		}

		[DerivedMessage(ProjectionMessage.CoreProcessing)]
		public sealed partial class RestartRequested : Message {
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

public static partial class CoreProjectionProcessingMessage {
	[DerivedMessage]
	public abstract partial class Message : EventStore.Core.Messaging.Message {
		private readonly Guid _projectionId;

		protected Message(Guid projectionId) {
			_projectionId = projectionId;
		}

		public Guid ProjectionId {
			get { return _projectionId; }
		}
	}

	[DerivedMessage(ProjectionMessage.CoreProcessing)]
	public partial class CheckpointLoaded : Message {
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

	[DerivedMessage(ProjectionMessage.CoreProcessing)]
	public partial class PrerecordedEventsLoaded : Message {
		private readonly CheckpointTag _checkpointTag;

		public PrerecordedEventsLoaded(Guid projectionId, CheckpointTag checkpointTag)
			: base(projectionId) {
			_checkpointTag = checkpointTag;
		}

		public CheckpointTag CheckpointTag {
			get { return _checkpointTag; }
		}
	}

	[DerivedMessage(ProjectionMessage.CoreProcessing)]
	public partial class CheckpointCompleted : Message {
		private readonly CheckpointTag _checkpointTag;

		public CheckpointCompleted(Guid projectionId, CheckpointTag checkpointTag)
			: base(projectionId) {
			_checkpointTag = checkpointTag;
		}

		public CheckpointTag CheckpointTag {
			get { return _checkpointTag; }
		}
	}

	[DerivedMessage(ProjectionMessage.CoreProcessing)]
	public partial class RestartRequested : Message {
		private readonly string _reason;

		public RestartRequested(Guid projectionId, string reason)
			: base(projectionId) {
			_reason = reason;
		}

		public string Reason {
			get { return _reason; }
		}
	}

	[DerivedMessage(ProjectionMessage.CoreProcessing)]
	public partial class Failed : Message {
		private readonly string _reason;

		public Failed(Guid projectionId, string reason)
			: base(projectionId) {
			_reason = reason;
		}

		public string Reason {
			get { return _reason; }
		}
	}

	[DerivedMessage(ProjectionMessage.CoreProcessing)]
	public partial class ReadyForCheckpoint : EventStore.Core.Messaging.Message {
		private readonly object _sender;

		public ReadyForCheckpoint(object sender) {
			_sender = sender;
		}

		public object Sender {
			get { return _sender; }
		}
	}

	[DerivedMessage(ProjectionMessage.CoreProcessing)]
	public partial class EmittedStreamAwaiting : EventStore.Core.Messaging.Message {
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

	[DerivedMessage(ProjectionMessage.CoreProcessing)]
	public partial class EmittedStreamWriteCompleted : EventStore.Core.Messaging.Message {
		private readonly string _streamId;

		public EmittedStreamWriteCompleted(string streamId) {
			_streamId = streamId;
		}

		public string StreamId {
			get { return _streamId; }
		}
	}
}
