using System.Runtime.InteropServices;

namespace EventStore.Core.Telemetry;

public class EnvironmentTelemetry {
	public string Arch { get; init; }
	public ContainerInfo Container { get; init; }
	public MachineInfo Machine { get; init; }

	public static EnvironmentTelemetry Collect(ClusterVNodeOptions options) {
		return new EnvironmentTelemetry {
			Arch = RuntimeInformation.ProcessArchitecture.ToString(),
			Container = ContainerInfo.Instance,
			Machine = MachineInfo.Collect(options),
		};
	}
}
