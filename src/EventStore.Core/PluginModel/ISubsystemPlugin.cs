
namespace EventStore.Core.PluginModel {
	public interface ISubsystemPlugin {
		string Name { get; }
		string Version { get; }

		string CommandLineName { get; }

		ISubsystemFactory GetSubsystemFactory();
	}
}
