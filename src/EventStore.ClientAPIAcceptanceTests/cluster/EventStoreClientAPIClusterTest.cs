using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;

namespace EventStore.ClientAPI.Tests {
	[Collection(nameof(EventStoreClientAPICollection))]
	public abstract class EventStoreClientAPITest {
		public string GetStreamName([CallerMemberName] string testMethod = default)
			=> $"{GetType().Name}_{testMethod ?? "unknown"}";

		protected static IEnumerable<bool> UseSsl => new[] {true, false};

		protected static IEnumerable<(long expectedVersion, string displayName)> ExpectedVersions
			=> new[] {
				((long)ExpectedVersion.Any, nameof(ExpectedVersion.Any)),
				(ExpectedVersion.NoStream, nameof(ExpectedVersion.NoStream))
			};

		public static IEnumerable<object[]> UseSslTestCases() => UseSsl.Select(useSsl => new object[] {useSsl});

		public static IEnumerable<object[]> ExpectedVersionTestCases() {
			foreach (var (expectedVersion, displayName) in ExpectedVersions)
			foreach (var useSsl in UseSsl) {
				yield return new object[] {expectedVersion, displayName, useSsl};
			}
		}
	}
}
