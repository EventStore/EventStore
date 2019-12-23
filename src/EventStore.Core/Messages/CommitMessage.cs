using System;
using System.Collections.Generic;
using System.Text;
using EventStore.Common.Utils;
using EventStore.Core.Messaging;

namespace EventStore.Core.Messages {
	public static class CommitMessage {
		
		public class ReplicaLogWrittenTo : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}
			
			public readonly long LogPosition;
			public readonly Guid ReplicaId;
			
			public ReplicaLogWrittenTo(long logPosition, Guid replicaId) {
				Ensure.Nonnegative(logPosition, nameof(logPosition));
				Ensure.NotEmptyGuid(replicaId, nameof(replicaId));
				LogPosition = logPosition;
				ReplicaId = replicaId;
			}
		}
		public class IndexWrittenTo : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}
			
			public readonly long LogPosition;
			
			public IndexWrittenTo(long logPosition ) {
				Ensure.Nonnegative(logPosition, "logPosition");
				LogPosition = logPosition;
			}
		}
		public class LogWrittenTo : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}
			
			public readonly long LogPosition;
			
			public LogWrittenTo(long logPosition ) {
				Ensure.Nonnegative(logPosition, "logPosition");
				LogPosition = logPosition;
			}
		}
		public class LogCommittedTo : Message { 
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}
			
			public readonly long LogPosition;
			
			public LogCommittedTo(long logPosition ) {
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
