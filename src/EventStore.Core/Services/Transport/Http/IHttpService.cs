using System;
using System.Collections.Generic;
using System.Net;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Transport.Http.EntityManagement;
using MidFunc = System.Func<
	Microsoft.AspNetCore.Http.HttpContext,
	System.Func<System.Threading.Tasks.Task>,
	System.Threading.Tasks.Task
>;

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

	public interface IHttpService {
		ServiceAccessibility Accessibility { get; }
		bool IsListening { get; }
		IEnumerable<IPEndPoint> EndPoints { get; }

		List<UriToActionMatch> GetAllUriMatches(Uri uri);
		void SetupController(IHttpController controller);

		void RegisterCustomAction(ControllerAction action,
			Func<HttpEntityManager, UriTemplateMatch, RequestParams> handler);

		void RegisterAction(ControllerAction action, Action<HttpEntityManager, UriTemplateMatch> handler);

		void Shutdown();

		MidFunc MidFunc { get; }
	}
}
