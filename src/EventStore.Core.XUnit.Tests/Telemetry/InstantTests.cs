using EventStore.Core.Time;
using Xunit;

namespace EventStore.Core.XUnit.Tests.Telemetry;

public class InstantTests {
	[Fact]
	public void can_measure_elapsed() {
		var x = Instant.FromSeconds(4);
		var y = Instant.FromSeconds(6);
		Assert.Equal(2, y.ElapsedSecondsSince(x));
	}
}
