using EventStore.Core.Authorization;

namespace EventStore.Core.PluginModel {
	public interface IAuthorizationPlugin {
		string Name { get; }
		string Version { get; }

		string CommandLineName { get; }

		IAuthorizationProviderFactory GetAuthorizationProviderFactory(string authorizationConfigPath);
	}
}
