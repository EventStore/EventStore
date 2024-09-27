using System.Linq;
using EventStore.Core.Bus;

namespace EventStore.Core.Authorization;

// TODO: Move this to EventStore.Plugins when we're decoupling the StreamPolicies Plugin
public interface IPolicySelectorFactory {
	public string CommandLineName { get; }
	public string Name { get; }
	public string Version { get; }
	public IPolicySelector Create(IPublisher publisher, ISubscriber subscriber);
}

public interface IPolicySelector {
	ReadOnlyPolicy Select();
}

public class PolicySelectorsFactory(params IPolicySelectorFactory[] policySelectorFactories) {
	public IPolicySelector[] Create(
		AuthorizationProviderFactoryComponents authorizationProviderFactoryComponents) =>
		policySelectorFactories
			.Select(
				p => p.Create(
					authorizationProviderFactoryComponents.MainQueue,
					authorizationProviderFactoryComponents.MainBus))
			.ToArray();
}
