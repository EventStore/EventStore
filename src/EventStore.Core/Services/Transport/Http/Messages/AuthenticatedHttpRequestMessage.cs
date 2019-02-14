using EventStore.Core.Messaging;
using EventStore.Transport.Http.EntityManagement;

namespace EventStore.Core.Services.Transport.Http.Messages {
	class AuthenticatedHttpRequestMessage : Message {
		private static readonly int TypeId = System.Threading.Interlocked.Increment(ref NextMsgId);

		public override int MsgTypeId {
			get { return TypeId; }
		}

		public readonly HttpService HttpService;
		public readonly HttpEntity Entity;

		public AuthenticatedHttpRequestMessage(HttpService httpService, HttpEntity entity) {
			HttpService = httpService;
			Entity = entity;
		}
	}
}
