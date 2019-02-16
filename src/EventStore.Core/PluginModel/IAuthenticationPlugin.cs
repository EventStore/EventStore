using EventStore.Core.Authentication;

namespace EventStore.Core.PluginModel {
	public interface IAuthenticationPlugin {
		string Name { get; }
		string Version { get; }

		string CommandLineName { get; }

		IAuthenticationProviderFactory GetAuthenticationProviderFactory(string authenticationConfigPath);
	}
}
