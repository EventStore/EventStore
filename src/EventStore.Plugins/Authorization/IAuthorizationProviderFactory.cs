namespace EventStore.Plugins.Authorization {
	public interface IAuthorizationProviderFactory {
		IAuthorizationProvider Build();
	}
}
