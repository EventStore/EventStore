using System;

namespace EventStore.Core.Telemetry {
	public interface IClock {
		DateTime UtcNow { get; }
		long SecondsSinceEpoch { get; }
	}

	public class Clock : IClock {
		public static readonly Clock Instance = new();
		public DateTime UtcNow => DateTime.UtcNow;
		public long SecondsSinceEpoch => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
	}
}
