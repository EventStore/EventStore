using System;
using EventStore.Core.Telemetry;

namespace EventStore.Core.XUnit.Tests.Telemetry {
	internal class FakeClock : IClock {
		public DateTime UtcNow => DateTimeOffset.FromUnixTimeSeconds(SecondsSinceEpoch).UtcDateTime;
		public long SecondsSinceEpoch { get; set; }
	}
}
