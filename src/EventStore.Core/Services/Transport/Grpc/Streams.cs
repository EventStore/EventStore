using System;
using EventStore.Core.Authentication;
using EventStore.Core.Bus;
using EventStore.Core.Services.Storage.ReaderIndex;

namespace EventStore.Core.Services.Transport.Grpc {
	public partial class Streams : EventStore.Client.Streams.Streams.StreamsBase {
		private readonly IQueuedHandler _queue;
		private readonly IReadIndex _readIndex;
		private readonly int _maxAppendSize;

		public Streams(IQueuedHandler queue, IReadIndex readIndex,
			int maxAppendSize) {
			if (queue == null) throw new ArgumentNullException(nameof(queue));

			_queue = queue;
			_readIndex = readIndex;
			_maxAppendSize = maxAppendSize;
		}
	}
}
