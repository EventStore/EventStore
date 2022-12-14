using EventStore.Core.Bus;
using EventStore.Plugins.Authorization;

namespace EventStore.Core.Services.Transport.Grpc {
	internal partial class Redaction : EventStore.Client.Redaction.Redaction.RedactionBase {
		private readonly IPublisher _bus;
		private readonly IAuthorizationProvider _authorizationProvider;

		public Redaction(IPublisher bus, IAuthorizationProvider authorizationProvider) {
			_bus = bus;
			_authorizationProvider = authorizationProvider;
		}
	}
}
