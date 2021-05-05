using System;
using System.Collections.Generic;
using System.Threading.Channels;
using System.Threading.Tasks;
using EventStore.Core.Data;
using Grpc.Core;

namespace EventStore.Core.Services.Transport.Grpc {
	public static partial class Enumerators {
		private const int MaxLiveEventBufferCount = 16;
		private const int ReadBatchSize = 32; // TODO  JPB make this configurable

		private static readonly BoundedChannelOptions BoundedChannelOptions =
			new BoundedChannelOptions(MaxLiveEventBufferCount) {
			FullMode = BoundedChannelFullMode.Wait,
			SingleReader = true,
			SingleWriter = true
		};

		private static bool ShouldIgnore(Exception ex) =>
			ex is OperationCanceledException ||
			ex is ChannelClosedException &&
			!(ex.InnerException is RpcException);

		public interface ISubscriptionEnumerator : IAsyncEnumerator<ResolvedEvent> {
			Task Started { get; }
			string SubscriptionId { get; }
		}
	}
}
