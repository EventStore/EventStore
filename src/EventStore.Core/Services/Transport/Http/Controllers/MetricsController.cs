using System;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;
using EventStore.Plugins.Authorization;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Codecs;

namespace EventStore.Core.Services.Transport.Http.Controllers {
	public class MetricsController : CommunicationController {
		private static readonly ICodec[] SupportedCodecs = new ICodec[] {
			Codec.CreateCustom(Codec.Text, "application/openmetrics-text", Helper.UTF8NoBom, false, false),
		};

		public MetricsController() : base(new NoOpPublisher()) {
		}

		protected override void SubscribeCore(IHttpService service) {
			Ensure.NotNull(service, "service");

			// this exists only to specify the permissions required for the /metrics endpoint
			// the PrometheusExporterMiddleware handles the request itself
			RegisterAuthOnly(service, "/metrics", HttpMethod.Get, Codec.NoCodecs, SupportedCodecs, Operations.Node.Statistics.Read);
		}

		class NoOpPublisher : IPublisher {
			public void Publish(Message message) {
			}
		}
	}
}
