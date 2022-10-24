using EventStore.Core.Bus;
using EventStore.Core.Messaging;
using EventStore.Transport.Http.EntityManagement;

namespace EventStore.Core.Services.Transport.Http.Messages {
	[StatsMessage(MessageType.IncomingHttpRequestMessage)]
	public partial class IncomingHttpRequestMessage : Message {
		public readonly IPublisher NextStagePublisher;
		public readonly IHttpService HttpService;
		public readonly HttpEntity Entity;

		public IncomingHttpRequestMessage(IHttpService httpService, HttpEntity entity, IPublisher nextStagePublisher) {
			HttpService = httpService;
			Entity = entity;
			NextStagePublisher = nextStagePublisher;
		}
	}
}
