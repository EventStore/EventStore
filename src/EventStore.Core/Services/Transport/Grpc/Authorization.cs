using EventStore.Plugins.Authorization;

namespace EventStore.Core.Services.Transport.Grpc;

internal partial class Authorization : EventStore.Client.Authorization.Authorization.AuthorizationBase {
	private readonly IAuthorizationProvider _provider;

	public Authorization(IAuthorizationProvider provider) {
		_provider = provider;
	}
}
