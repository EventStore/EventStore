using EventStore.Core.Bus;
using EventStore.Core.Messaging;
using EventStore.Transport.Http.EntityManagement;

namespace EventStore.Core.Services.Transport.Http.Messages {
	public class IncomingHttpRequestMessage : Message {
		private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

		public override int MsgTypeId {
			get { return TypeId; }
		}

		public readonly IPublisher NextStagePublisher;
		public readonly HttpService HttpService;
		public readonly HttpEntity Entity;

		public IncomingHttpRequestMessage(HttpService httpService, HttpEntity entity, IPublisher nextStagePublisher) {
			HttpService = httpService;
			Entity = entity;
			NextStagePublisher = nextStagePublisher;
		}
	}
}
