using System.Collections.Generic;
using System.Threading.Tasks;
using EventStore.Core.Data;

namespace EventStore.Core.Services.Transport.Grpc {
	internal static partial class Enumerators {
		private const int MaxLiveEventBufferCount = 16;
		private const int ReadBatchSize = 32; // TODO  JPB make this configurable
		public interface ISubscriptionEnumerator : IAsyncEnumerator<ResolvedEvent> {
			Task Started { get; }
			string SubscriptionId { get; }
		}
	}
}
