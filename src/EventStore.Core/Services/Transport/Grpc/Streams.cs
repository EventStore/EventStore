using System;
using EventStore.Core.Authentication;
using EventStore.Core.Bus;
using EventStore.Core.Services.Storage.ReaderIndex;

namespace EventStore.Core.Services.Transport.Grpc {
	public partial class Streams : EventStore.Grpc.Streams.Streams.StreamsBase {
		private readonly IQueuedHandler _queue;
		private readonly IReadIndex _readIndex;
		private readonly IAuthenticationProvider _authenticationProvider;

		public Streams(IQueuedHandler queue, IAuthenticationProvider authenticationProvider, IReadIndex readIndex) {
			if (queue == null) throw new ArgumentNullException(nameof(queue));
			if (authenticationProvider == null) throw new ArgumentNullException(nameof(authenticationProvider));

			_queue = queue;
			_readIndex = readIndex;
			_authenticationProvider = authenticationProvider;
		}
	}
}
