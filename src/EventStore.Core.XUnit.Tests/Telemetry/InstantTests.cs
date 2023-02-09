using System;
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

	[Fact]
	public void add() {
		Assert.Equal(
			Instant.FromSeconds(4),
			Instant.FromSeconds(3).Add(TimeSpan.FromSeconds(1)));
	}

	[Fact]
	public void equal_to() {
		Assert.False(Instant.FromSeconds(3) == Instant.FromSeconds(4));
		Assert.True(Instant.FromSeconds(3) == Instant.FromSeconds(3));
	}

	[Fact]
	public void not_equal_to() {
		Assert.True(Instant.FromSeconds(3) != Instant.FromSeconds(4));
		Assert.False(Instant.FromSeconds(3) != Instant.FromSeconds(3));
	}

	[Fact]
	public void less_than() {
		Assert.True(Instant.FromSeconds(3) < Instant.FromSeconds(4));
		Assert.False(Instant.FromSeconds(3) < Instant.FromSeconds(3));
		Assert.False(Instant.FromSeconds(3) < Instant.FromSeconds(2));
	}

	[Fact]
	public void greater_than() {
		Assert.False(Instant.FromSeconds(3) > Instant.FromSeconds(4));
		Assert.False(Instant.FromSeconds(3) > Instant.FromSeconds(3));
		Assert.True(Instant.FromSeconds(3) > Instant.FromSeconds(2));
	}

	[Fact]
	public void less_than_or_equal_to() {
		Assert.True(Instant.FromSeconds(3) <= Instant.FromSeconds(4));
		Assert.True(Instant.FromSeconds(3) <= Instant.FromSeconds(3));
		Assert.False(Instant.FromSeconds(3) <= Instant.FromSeconds(2));
	}

	[Fact]
	public void greater_than_or_equal_to() {
		Assert.False(Instant.FromSeconds(3) >= Instant.FromSeconds(4));
		Assert.True(Instant.FromSeconds(3) >= Instant.FromSeconds(3));
		Assert.True(Instant.FromSeconds(3) >= Instant.FromSeconds(2));
	}
}
