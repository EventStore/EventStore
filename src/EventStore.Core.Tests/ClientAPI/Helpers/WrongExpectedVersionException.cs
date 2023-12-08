using System;

namespace EventStore.Core.Tests.ClientAPI.Helpers;

public class WrongExpectedVersionException : Exception {
	public WrongExpectedVersionException(long expectedVersion, long actualVersion) {
		ExpectedVersion = expectedVersion;
		ActualVersion = actualVersion;
	}

	public long ExpectedVersion { get; init; }
	public long ActualVersion { get; init; }

	public void Deconstruct(out long expectedVersion, out long actualVersion) {
		expectedVersion = ExpectedVersion;
		actualVersion = ActualVersion;
	}
}
