using System;
using EventStore.Plugins.Subsystems;

namespace EventStore.Core {
	public interface ISubsystemFactory : ISubsystemFactory<StandardComponents> {
	}

	public class SubsystemFactoryAdapter<T> : ISubsystemFactory {
		private readonly ISubsystemFactory<T> _wrapped;
		private readonly Func<StandardComponents, T> _adapt;

		public SubsystemFactoryAdapter(ISubsystemFactory<T> wrapped, Func<StandardComponents, T> adapt) {
			_adapt = adapt;
			_wrapped = wrapped;
		}

		public ISubsystem Create(StandardComponents components) =>
			_wrapped.Create(_adapt(components));
	}
}
