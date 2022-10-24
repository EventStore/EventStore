using System;
using System.Net.Sockets;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Transport.Tcp;

namespace EventStore.Core.Messages {
	public static partial class TcpMessage {
		[StatsGroup("tcp")]
		public enum MessageType {
			None = 0,
			TcpSend = 1,
			Heartbeat = 2,
			HeartbeatTimeout = 3,
			PongMessage = 4,
			ConnectionEstablished = 5,
			ConnectionClosed = 6,
			NotReady = 7,
			NotAuthenticated = 8,
			Authenticated = 9,
		}

		[StatsMessage(MessageType.TcpSend)]
		public partial class TcpSend : Message, IQueueAffineMessage {
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

		[StatsMessage(MessageType.Heartbeat)]
		public partial class Heartbeat : Message {
			public readonly long ReceiveProgressIndicator;
			public readonly long SendProgressIndicator;

			public Heartbeat(long receiveProgressIndicator, long sendProgressIndicator) {
				ReceiveProgressIndicator = receiveProgressIndicator;
				SendProgressIndicator = sendProgressIndicator;
			}
		}

		[StatsMessage(MessageType.HeartbeatTimeout)]
		public partial class HeartbeatTimeout : Message {
			public readonly long ReceiveProgressIndicator;

			public HeartbeatTimeout(long receiveProgressIndicator) {
				ReceiveProgressIndicator = receiveProgressIndicator;
			}
		}

		[StatsMessage(MessageType.PongMessage)]
		public partial class PongMessage : Message {
			public readonly Guid CorrelationId;
			public readonly byte[] Payload;

			public PongMessage(Guid correlationId, byte[] payload) {
				CorrelationId = correlationId;
				Payload = payload;
			}
		}

		[StatsMessage(MessageType.ConnectionEstablished)]
		public partial class ConnectionEstablished : Message {
			public readonly TcpConnectionManager Connection;

			public ConnectionEstablished(TcpConnectionManager connection) {
				Connection = connection;
			}
		}

		[StatsMessage(MessageType.ConnectionClosed)]
		public partial class ConnectionClosed : Message {
			public readonly TcpConnectionManager Connection;
			public readonly SocketError SocketError;

			public ConnectionClosed(TcpConnectionManager connection, SocketError socketError) {
				Connection = connection;
				SocketError = socketError;
			}
		}

		[StatsMessage(MessageType.NotReady)]
		public partial class NotReady : Message {
			public readonly Guid CorrelationId;
			public readonly string Reason;

			public NotReady(Guid correlationId, string reason) {
				CorrelationId = correlationId;
				Reason = reason;
			}
		}


		[StatsMessage(MessageType.NotAuthenticated)]
		public partial class NotAuthenticated : Message {
			public readonly Guid CorrelationId;
			public readonly string Reason;

			public NotAuthenticated(Guid correlationId, string reason) {
				CorrelationId = correlationId;
				Reason = reason;
			}
		}

		[StatsMessage(MessageType.Authenticated)]
		public partial class Authenticated : Message {
			public readonly Guid CorrelationId;

			public Authenticated(Guid correlationId) {
				CorrelationId = correlationId;
			}
		}
	}
}
