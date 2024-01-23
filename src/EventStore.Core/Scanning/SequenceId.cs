using System.Threading;

namespace EventStore.Core.Scanning;

public static class SequenceId<T> {
	// ReSharper disable once StaticMemberInGenericType
	private static int _actual = -1;

	public static int Next() => Interlocked.Increment(ref _actual);
}

public static class SequenceId {
	public static int Next() => SequenceId<object>.Next();
}
