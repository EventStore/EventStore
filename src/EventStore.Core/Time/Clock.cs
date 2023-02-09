using System;

namespace EventStore.Core.Time {
	public interface IClock {
		Instant Now { get; }
		long SecondsSinceEpoch { get; }
	}

	public class Clock : IClock {
		public static readonly Clock Instance = new();
		private Clock() { }
		public Instant Now => Instant.Now;
		public long SecondsSinceEpoch => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
	}
}
