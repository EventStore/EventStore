using System;
using System.Collections.Generic;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Transport.Http.EntityManagement;

namespace EventStore.Core.Services.Transport.Http {
	public interface IHttpController {
		void Subscribe(IHttpService service);
	}

	public interface IHttpSender {
		void SubscribeSenders(HttpMessagePipe pipe);
	}

	public interface IHttpForwarder {
		bool ForwardRequest(HttpEntityManager manager);
	}

	public interface IHttpService :
		IHandle<SystemMessage.SystemInit>,
		IHandle<SystemMessage.BecomeShuttingDown>,
		IHandle<HttpMessage.PurgeTimedOutRequests> {
		IEnumerable<string> ListenPrefixes { get; }
		ServiceAccessibility Accessibility { get; }
		bool IsListening { get; }

		List<UriToActionMatch> GetAllUriMatches(Uri uri);
		void SetupController(IHttpController controller);

		void RegisterCustomAction(ControllerAction action,
			Func<HttpEntityManager, UriTemplateMatch, RequestParams> handler);

		void RegisterAction(ControllerAction action, Action<HttpEntityManager, UriTemplateMatch> handler);

		void Shutdown();
	}
}
