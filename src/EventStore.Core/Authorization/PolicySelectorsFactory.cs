using System.Linq;
using EventStore.Core.Bus;

namespace EventStore.Core.Authorization;

// TODO: Should this be moved to EventStore.Plugins?
public interface IPolicySelectorFactory {
	public string CommandLineName { get; }
	public string Name { get; }
	public string Version { get; }
	public IPolicySelector Create(IPublisher publisher, ISubscriber subscriber);
}

public interface IStreamPermissionAssertion : IAssertion {
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
