using System;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;
using EventStore.Transport.Http;
using EventStore.Transport.Http.Codecs;
using EventStore.Transport.Http.EntityManagement;

namespace EventStore.Core.Services.Transport.Http.Controllers {
	public abstract class CommunicationController : IHttpController {
		private static readonly ILogger Log = LogManager.GetLoggerFor<CommunicationController>();
		private static readonly ICodec[] DefaultCodecs = new ICodec[] {Codec.Json, Codec.Xml};

		private readonly IPublisher _publisher;

		protected CommunicationController(IPublisher publisher) {
			Ensure.NotNull(publisher, "publisher");

			_publisher = publisher;
		}

		public void Publish(Message message) {
			Ensure.NotNull(message, "message");
			_publisher.Publish(message);
		}

		public void Subscribe(IHttpService service) {
			Ensure.NotNull(service, "service");
			SubscribeCore(service);
		}

		protected abstract void SubscribeCore(IHttpService service);

		protected RequestParams SendBadRequest(HttpEntityManager httpEntityManager, string reason) {
			httpEntityManager.ReplyStatus(HttpStatusCode.BadRequest,
				reason,
				e => Log.Debug("Error while closing HTTP connection (bad request): {e}.", e.Message));
			return new RequestParams(done: true);
		}

		protected RequestParams SendTooBig(HttpEntityManager httpEntityManager) {
			httpEntityManager.ReplyStatus(HttpStatusCode.RequestEntityTooLarge,
				"Too large events received. Limit is 4mb",
				e => Log.Debug("Too large events received over HTTP"));
			return new RequestParams(done: true);
		}

		protected RequestParams SendOk(HttpEntityManager httpEntityManager) {
			httpEntityManager.ReplyStatus(HttpStatusCode.OK,
				"OK",
				e => Log.Debug("Error while closing HTTP connection (ok): {e}.", e.Message));
			return new RequestParams(done: true);
		}

		protected void Register(IHttpService service, string uriTemplate, string httpMethod,
			Action<HttpEntityManager, UriTemplateMatch> handler, ICodec[] requestCodecs, ICodec[] responseCodecs) {
			service.RegisterAction(new ControllerAction(uriTemplate, httpMethod, requestCodecs, responseCodecs),
				handler);
		}

		protected void RegisterCustom(IHttpService service, string uriTemplate, string httpMethod,
			Func<HttpEntityManager, UriTemplateMatch, RequestParams> handler,
			ICodec[] requestCodecs, ICodec[] responseCodecs) {
			service.RegisterCustomAction(new ControllerAction(uriTemplate, httpMethod, requestCodecs, responseCodecs),
				handler);
		}

		protected void RegisterUrlBased(IHttpService service, string uriTemplate, string httpMethod,
			Action<HttpEntityManager, UriTemplateMatch> action) {
			Register(service, uriTemplate, httpMethod, action, Codec.NoCodecs, DefaultCodecs);
		}

		protected static string MakeUrl(HttpEntityManager http, string path) {
			if (path.Length > 0 && path[0] == '/') path = path.Substring(1);
			var hostUri = http.ResponseUrl;
			var builder = new UriBuilder(hostUri.Scheme, hostUri.Host, hostUri.Port, hostUri.LocalPath + path);
			return builder.Uri.AbsoluteUri;
		}
	}
}
