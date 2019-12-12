
namespace EventStore.Core.PluginModel {
	public interface ISubsystemPlugin {
		string Name { get; }
		string Version { get; }

		ISubsystemFactory GetSubsystemFactory();
	}
}
