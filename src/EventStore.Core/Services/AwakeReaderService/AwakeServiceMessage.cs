using System;
using EventStore.Core.Data;
using EventStore.Core.Messaging;

namespace EventStore.Core.Services.AwakeReaderService {
	public class AwakeServiceMessage {
		public sealed class SubscribeAwake : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly IEnvelope Envelope;
			public readonly Guid CorrelationId;
			public readonly string StreamId;
			public readonly TFPos From;
			public readonly Message ReplyWithMessage;

			public SubscribeAwake(
				IEnvelope envelope, Guid correlationId, string streamId, TFPos @from, Message replyWithMessage) {
				StreamId = streamId;
				From = @from;
				ReplyWithMessage = replyWithMessage;
				Envelope = envelope;
				CorrelationId = correlationId;
			}
		}

		public sealed class UnsubscribeAwake : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid CorrelationId;

			public UnsubscribeAwake(Guid correlationId) {
				CorrelationId = correlationId;
			}
		}
	}
}
