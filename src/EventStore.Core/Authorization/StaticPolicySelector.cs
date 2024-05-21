namespace EventStore.Core.Authorization;

public class StaticPolicySelector(ReadOnlyPolicy policy) : IPolicySelector {
	public ReadOnlyPolicy Select() => policy;
}
