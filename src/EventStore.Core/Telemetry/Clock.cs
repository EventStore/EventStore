using System;

namespace EventStore.Core.Telemetry {
	public interface IClock {
		long SecondsSinceEpoch { get; }
	}

	public class Clock : IClock {
		public static readonly Clock Instance = new();
		public long SecondsSinceEpoch => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
	}
}
