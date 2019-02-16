using System;
using EventStore.Core.Messaging;

namespace EventStore.Core.Services.TimerService {
	public static class TimerMessage {
		public class Schedule : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly TimeSpan TriggerAfter;

			public readonly IEnvelope Envelope;
			public readonly Message ReplyMessage;

			private readonly Action _replyAction;

			public static Schedule Create<T>(TimeSpan triggerAfter, IEnvelope envelope, T replyMessage)
				where T : Message {
				return new Schedule(triggerAfter, envelope, replyMessage, () => envelope.ReplyWith(replyMessage));
			}

			private Schedule(TimeSpan triggerAfter, IEnvelope envelope, Message replyMessage, Action replyAction) {
				if (envelope == null)
					throw new ArgumentNullException("envelope");
				if (replyMessage == null)
					throw new ArgumentNullException("replyMessage");
				if (replyAction == null)
					throw new ArgumentNullException("replyAction");

				TriggerAfter = triggerAfter;
				Envelope = envelope;
				ReplyMessage = replyMessage;
				_replyAction = replyAction;
			}

			public void Reply() {
				_replyAction();
			}
		}
	}
}
