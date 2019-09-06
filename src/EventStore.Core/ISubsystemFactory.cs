
namespace EventStore.Core {
	public interface ISubsystemFactory {
		ISubsystem Create(string configPath);
	}
}