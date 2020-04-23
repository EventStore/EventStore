using System;
using EventStore.Core.Messaging;
using EventStore.Core.Services.Transport.Http;
using EventStore.Transport.Http.EntityManagement;

namespace EventStore.Core.Messages {
	public enum DenialReason {
		ServerTooBusy
	}

	public static class HttpMessage {
		public abstract class HttpSendMessage : Message, IQueueAffineMessage {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public int QueueId {
				get { return HttpEntityManager.GetHashCode(); }
			}

			public readonly IEnvelope Envelope;
			public readonly Guid CorrelationId;
			public readonly HttpEntityManager HttpEntityManager;

			/// <param name="correlationId"></param>
			/// <param name="envelope">non-null envelope requests HttpCompleted messages in response</param>
			/// <param name="httpEntityManager"></param>
			protected HttpSendMessage(Guid correlationId, IEnvelope envelope, HttpEntityManager httpEntityManager) {
				CorrelationId = correlationId;
				Envelope = envelope;
				HttpEntityManager = httpEntityManager;
			}
		}

		public class HttpSend : HttpSendMessage {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly object Data;
			public readonly ResponseConfiguration Configuration;
			public readonly Message Message;

			public HttpSend(
				HttpEntityManager httpEntityManager, ResponseConfiguration configuration, object data, Message message)
				: base(Guid.Empty, null, httpEntityManager) {
				Data = data;
				Configuration = configuration;
				Message = message;
			}
		}

		public class DeniedToHandle : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly DenialReason Reason;
			public readonly string Details;

			public DeniedToHandle(DenialReason reason, string details) {
				Reason = reason;
				Details = details;
			}
		}

		public class PurgeTimedOutRequests : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}
		}

		public class TextMessage : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public string Text { get; set; }

			public TextMessage() {
			}

			public TextMessage(string text) {
				Text = text;
			}

			public override string ToString() {
				return string.Format("Text: {0}", Text);
			}
		}
	}
}
