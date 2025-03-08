using System.Security.Claims;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;

namespace KurrentDB.UI.Services;

// This is a client-side AuthenticationStateProvider that determines the user's authentication state by
// looking for data persisted in the page when it was rendered on the server. This authentication state will
// be fixed for the lifetime of the WebAssembly application. So, if the user needs to log in or out, a full
// page reload is required.
//
// This only provides a username and email for display purposes. It does not actually include any tokens
// that authenticate to the server when making subsequent requests. That works separately using a
// cookie that will be included on HttpClient requests to the server.
class PersistentAuthenticationStateProvider : AuthenticationStateProvider {
	static readonly Task<AuthenticationState> DefaultUnauthenticatedTask = Task.FromResult(new AuthenticationState(new(new ClaimsIdentity())));

	readonly Task<AuthenticationState> _authenticationStateTask = DefaultUnauthenticatedTask;

	public PersistentAuthenticationStateProvider(PersistentComponentState state) {
		if (!state.TryTakeFromJson<UserInfo>(nameof(UserInfo), out var userInfo) || userInfo is null) {
			return;
		}

		Claim[] claims = [
			new(ClaimTypes.NameIdentifier, userInfo.UserId),
			new(ClaimTypes.Name, userInfo.UserId),
		];

		_authenticationStateTask = Task.FromResult(new AuthenticationState(new(new ClaimsIdentity(claims, authenticationType: nameof(PersistentAuthenticationStateProvider)))));
	}

	public override Task<AuthenticationState> GetAuthenticationStateAsync() => _authenticationStateTask;
}
