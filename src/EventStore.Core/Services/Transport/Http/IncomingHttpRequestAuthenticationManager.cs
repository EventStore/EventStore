using EventStore.Core.Bus;
using EventStore.Core.Services.Transport.Http.Authentication;
using EventStore.Core.Services.Transport.Http.Messages;
using NLog.Fluent;

namespace EventStore.Core.Services.Transport.Http {
	class IncomingHttpRequestAuthenticationManager : IHandle<IncomingHttpRequestMessage> {
		private readonly HttpAuthenticationProvider[] _providers;

		public IncomingHttpRequestAuthenticationManager(HttpAuthenticationProvider[] providers) {
			_providers = providers;
		}

		public void Handle(IncomingHttpRequestMessage message) {
			Authenticate(message);
		}

		private void Authenticate(IncomingHttpRequestMessage message) {
			try {
				foreach (var provider in _providers) {
					if (provider.Authenticate(message))
						return;
				}
			} catch {
				// TODO: JPB Log this as an error? Warning?
			}

			HttpAuthenticationProvider.ReplyUnauthorized(message.Entity);
		}
	}
}
