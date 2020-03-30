namespace EventStore.Plugins.Authentication {
	public interface IAuthenticationPlugin {
		string Name { get; }
		string Version { get; }

		string CommandLineName { get; }

		IAuthenticationProviderFactory GetAuthenticationProviderFactory(string authenticationConfigPath);
	}
}
