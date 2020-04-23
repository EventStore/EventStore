using System;
using System.Net;
using EventStore.Core.Messaging;

namespace EventStore.Core.Messages {
	public static class GrpcMessage {
		public class SendOverGrpc : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly IPEndPoint DestinationEndpoint;
			public readonly Message Message;
			public readonly DateTime LiveUntil;

			public SendOverGrpc(IPEndPoint destinationEndpoint, Message message, DateTime liveUntil) {
				DestinationEndpoint = destinationEndpoint;
				Message = message;
				LiveUntil = liveUntil;
			}
		}
	}
}
