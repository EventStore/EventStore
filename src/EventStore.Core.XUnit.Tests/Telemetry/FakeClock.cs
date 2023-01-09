using EventStore.Core.Telemetry;

namespace EventStore.Core.XUnit.Tests.Telemetry {
	internal class FakeClock : IClock {
		public long SecondsSinceEpoch { get; set; }
	}
}
