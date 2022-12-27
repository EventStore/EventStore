using System.Threading;
using EventStore.Common.Utils;
using EventStore.Core.Data;
using EventStore.Core.Data.Redaction;
using EventStore.Core.Messaging;

namespace EventStore.Core.Messages {
	public static class RedactionMessage {
		public class ReadEventInfo : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public IEnvelope Envelope { get; }
			public string EventStreamId { get; }
			public long EventNumber { get; }

			public ReadEventInfo(IEnvelope envelope, string eventStreamId, long eventNumber) {
				Ensure.NotNull(envelope, nameof(envelope));
				Ensure.NotNullOrEmpty(eventStreamId, nameof(eventStreamId));
				Ensure.Nonnegative(eventNumber, nameof(eventNumber));

				Envelope = envelope;
				EventStreamId = eventStreamId;
				EventNumber = eventNumber;
			}
		}

		public class ReadEventInfoCompleted : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public ReadEventInfoResult Result { get; }
			public EventInfo[] EventInfos { get; }

			public ReadEventInfoCompleted(ReadEventInfoResult result, EventInfo[] eventInfos) {
				Ensure.NotNull(eventInfos, nameof(eventInfos));

				Result = result;
				EventInfos = eventInfos;
			}
		}

		public class SwitchChunkLock : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public IEnvelope Envelope { get; }

			public SwitchChunkLock(IEnvelope envelope) {
				Envelope = envelope;
			}
		}

		public class SwitchChunkLockCompleted : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public SwitchChunkLockResult Result { get; }

			public SwitchChunkLockCompleted(SwitchChunkLockResult result) {
				Result = result;
			}
		}

		public class SwitchChunk : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public IEnvelope Envelope { get; }
			public string TargetChunkFile { get; }
			public string NewChunkFile { get; }

			public SwitchChunk(IEnvelope envelope, string targetChunkFile, string newChunkFile) {
				Ensure.NotNull(envelope, nameof(envelope));
				Ensure.NotNullOrEmpty(targetChunkFile, nameof(targetChunkFile));
				Ensure.NotNullOrEmpty(newChunkFile, nameof(newChunkFile));

				Envelope = envelope;
				TargetChunkFile = targetChunkFile;
				NewChunkFile = newChunkFile;
			}
		}

		public class SwitchChunkCompleted : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public SwitchChunkResult Result { get; }

			public SwitchChunkCompleted(SwitchChunkResult result) {
				Result = result;
			}
		}

		public class SwitchChunkUnlock : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public IEnvelope Envelope { get; }

			public SwitchChunkUnlock(IEnvelope envelope) {
				Envelope = envelope;
			}
		}

		public class SwitchChunkUnlockCompleted : Message {
			private static readonly int TypeId = Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public SwitchChunkUnlockResult Result { get; }

			public SwitchChunkUnlockCompleted(SwitchChunkUnlockResult result) {
				Result = result;
			}
		}
	}
}
