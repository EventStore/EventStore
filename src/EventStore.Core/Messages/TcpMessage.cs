using System;
using System.Net.Sockets;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Transport.Tcp;

namespace EventStore.Core.Messages {
	public static class TcpMessage {
		public class TcpSend : Message, IQueueAffineMessage {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public int QueueId {
				get { return ConnectionManager.GetHashCode(); }
			}

			public readonly TcpConnectionManager ConnectionManager;
			public readonly Message Message;

			public TcpSend(TcpConnectionManager connectionManager, Message message) {
				ConnectionManager = connectionManager;
				Message = message;
			}
		}

		public class Heartbeat : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly int MessageNumber;

			public Heartbeat(int messageNumber) {
				MessageNumber = messageNumber;
			}
		}

		public class HeartbeatTimeout : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly int MessageNumber;

			public HeartbeatTimeout(int messageNumber) {
				MessageNumber = messageNumber;
			}
		}

		public class PongMessage : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid CorrelationId;
			public readonly byte[] Payload;

			public PongMessage(Guid correlationId, byte[] payload) {
				CorrelationId = correlationId;
				Payload = payload;
			}
		}

		public class ConnectionEstablished : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly TcpConnectionManager Connection;

			public ConnectionEstablished(TcpConnectionManager connection) {
				Connection = connection;
			}
		}

		public class ConnectionClosed : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly TcpConnectionManager Connection;
			public readonly SocketError SocketError;

			public ConnectionClosed(TcpConnectionManager connection, SocketError socketError) {
				Connection = connection;
				SocketError = socketError;
			}
		}

		public class NotReady : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid CorrelationId;
			public readonly string Reason;

			public NotReady(Guid correlationId, string reason) {
				CorrelationId = correlationId;
				Reason = reason;
			}
		}


		public class NotAuthenticated : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid CorrelationId;
			public readonly string Reason;

			public NotAuthenticated(Guid correlationId, string reason) {
				CorrelationId = correlationId;
				Reason = reason;
			}
		}

		public class Authenticated : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid CorrelationId;

			public Authenticated(Guid correlationId) {
				CorrelationId = correlationId;
			}
		}
	}
}
