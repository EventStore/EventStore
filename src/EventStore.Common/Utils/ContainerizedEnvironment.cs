using System;

namespace EventStore.Common.Utils;

public static class ContainerizedEnvironment {

	public const int ReaderThreadCount = 4;
	public const int WorkerThreadCount = 5;
	public const int StreamInfoCacheCapacity = 100000;

	private static readonly string _dotnetContainerEnv;

	static ContainerizedEnvironment() {
		_dotnetContainerEnv = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER");
	}

	public static bool IsRunningInContainer() {
		return !string.IsNullOrEmpty(_dotnetContainerEnv);
	}
}
