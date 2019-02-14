using System;
using System.Net;
using EventStore.Common.Utils;
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

		public class HttpBeginSend : HttpSendMessage {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly ResponseConfiguration Configuration;

			public HttpBeginSend(Guid correlationId, IEnvelope envelope,
				HttpEntityManager httpEntityManager, ResponseConfiguration configuration)
				: base(correlationId, envelope, httpEntityManager) {
				Configuration = configuration;
			}
		}

		public class HttpSendPart : HttpSendMessage {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly string Data;

			public HttpSendPart(Guid correlationId, IEnvelope envelope, HttpEntityManager httpEntityManager,
				string data)
				: base(correlationId, envelope, httpEntityManager) {
				Data = data;
			}
		}

		public class HttpEndSend : HttpSendMessage {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public HttpEndSend(Guid correlationId, IEnvelope envelope, HttpEntityManager httpEntityManager)
				: base(correlationId, envelope, httpEntityManager) {
			}
		}

		public class HttpCompleted : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly Guid CorrelationId;
			public readonly HttpEntityManager HttpEntityManager;

			public HttpCompleted(Guid correlationId, HttpEntityManager httpEntityManager) {
				CorrelationId = correlationId;
				HttpEntityManager = httpEntityManager;
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

		public class SendOverHttp : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly IPEndPoint EndPoint;
			public readonly Message Message;
			public readonly DateTime LiveUntil;

			public SendOverHttp(IPEndPoint endPoint, Message message, DateTime liveUntil) {
				EndPoint = endPoint;
				Message = message;
				LiveUntil = liveUntil;
			}
		}

		public class PurgeTimedOutRequests : Message {
			private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

			public override int MsgTypeId {
				get { return TypeId; }
			}

			public readonly ServiceAccessibility Accessibility;

			public PurgeTimedOutRequests(ServiceAccessibility accessibility) {
				Accessibility = accessibility;
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
