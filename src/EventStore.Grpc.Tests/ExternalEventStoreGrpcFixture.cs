using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace EventStore.Grpc.Tests {
	public abstract class ExternalEventStoreGrpcFixture : StandaloneKestrelServerFixture, IAsyncLifetime {
		public const string TestEventType = "-";

		public virtual async Task InitializeAsync() {
			await Given().WithTimeout(TimeSpan.FromSeconds(5));
			await When().WithTimeout(TimeSpan.FromSeconds(5));
		}

		public new EventStoreGrpcClient Client => StandaloneKestrelServerFixture.Client;

		public virtual Task DisposeAsync() => Task.CompletedTask;

		protected abstract Task Given();
		protected abstract Task When();

		public IEnumerable<EventData> CreateTestEvents(int count = 1, string type = default)
			=> Enumerable.Range(0, count).Select(index => CreateTestEvent(index, type ?? TestEventType));

		protected static EventData CreateTestEvent(int index) => CreateTestEvent(index, TestEventType);

		protected static EventData CreateTestEvent(int index, string type)
			=> new EventData(Uuid.NewUuid(), type, Encoding.UTF8.GetBytes($@"{{""x"":{index}}}"));

		public string GetStreamName([CallerMemberName] string testMethod = default) {
			var type = GetType();

			return $"{type.DeclaringType.Name}_{testMethod ?? "unknown"}";
		}
	}

	[CollectionDefinition(nameof(StandaloneKestrelServerTestCollection))]
	public class StandaloneKestrelServerTestCollection : ICollectionFixture<StandaloneKestrelServerFixture> {
	}
}
