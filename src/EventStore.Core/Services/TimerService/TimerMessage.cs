using System;
using EventStore.Core.Messaging;

namespace EventStore.Core.Services.TimerService {
	public static partial class TimerMessage {
		[DerivedMessage(CoreMessage.Timer)]
		public partial class Schedule : Message {
			public readonly TimeSpan TriggerAfter;

			private readonly object _envelope;
			public readonly Message ReplyMessage;

			private readonly Action _replyAction;

			public static Schedule Create<T>(TimeSpan triggerAfter, IEnvelope<T> envelope, T replyMessage)
				where T : Message {
				return new Schedule(triggerAfter, envelope, replyMessage, () => envelope.ReplyWith(replyMessage));
			}

			private Schedule(TimeSpan triggerAfter, object envelope, Message replyMessage, Action replyAction) {
				if (envelope == null)
					throw new ArgumentNullException("envelope");
				if (replyMessage == null)
					throw new ArgumentNullException("replyMessage");
				if (replyAction == null)
					throw new ArgumentNullException("replyAction");

				TriggerAfter = triggerAfter;
				_envelope = envelope;
				ReplyMessage = replyMessage;
				_replyAction = replyAction;
			}

			public void Reply() {
				_replyAction();
			}

			// Legacy: one piece of test infrastructure wants to rely with different messages
			public void ReplyWithDangerous<T>(T message) where T : Message {
				(_envelope as IEnvelope<T>).ReplyWith(message);
			}
		}
	}
}
