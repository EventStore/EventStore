namespace EventStore.Core.Authorization {
	public interface IAuthorizationProviderFactory {
		IAuthorizationProvider Build();
	}
}
