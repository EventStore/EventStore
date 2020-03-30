namespace EventStore.Plugins.Authorization {
	public interface IAuthorizationPlugin {
		string Name { get; }
		string Version { get; }

		string CommandLineName { get; }

		IAuthorizationProviderFactory GetAuthorizationProviderFactory(string authorizationConfigPath);
	}
}
