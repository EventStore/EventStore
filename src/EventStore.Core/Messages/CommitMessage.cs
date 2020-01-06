using System;
using System.Collections.Generic;
using System.Text;
using EventStore.Common.Utils;
using EventStore.Core.Messaging;

namespace EventStore.Core.Messages {
	public static class CommitMessage {
		
		public class ReplicaWrittenTo : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}
			
			public readonly long LogPosition;
			public readonly Guid ReplicaId;
			
			public ReplicaWrittenTo(long logPosition, Guid replicaId) {
				Ensure.Nonnegative(logPosition, nameof(logPosition));
				Ensure.NotEmptyGuid(replicaId, nameof(replicaId));
				LogPosition = logPosition;
				ReplicaId = replicaId;
			}
		}
		public class IndexedTo : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}
			
			public readonly long LogPosition;
			
			public IndexedTo(long logPosition ) {
				Ensure.Nonnegative(logPosition, "logPosition");
				LogPosition = logPosition;
			}
		}
		public class WrittenTo : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}
			
			public readonly long LogPosition;
			
			public WrittenTo(long logPosition ) {
				Ensure.Nonnegative(logPosition, "logPosition");
				LogPosition = logPosition;
			}
		}
		public class ReplicatedTo : Message { 
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}
			
			public readonly long LogPosition;
			
			public ReplicatedTo(long logPosition ) {
				Ensure.Nonnegative(logPosition, "logPosition");
				LogPosition = logPosition;
			}
		}
		public class CommittedTo : Message { 
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}
			
			public readonly long LogPosition;
			
			public CommittedTo(long logPosition ) {
				Ensure.Nonnegative(logPosition, "logPosition");
				LogPosition = logPosition;
			}
		}
		
	}
}
