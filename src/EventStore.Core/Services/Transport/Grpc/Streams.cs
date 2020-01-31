using System;
using EventStore.Core.Authentication;
using EventStore.Core.Bus;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Core.Services.TimerService;

namespace EventStore.Core.Services.Transport.Grpc {
	public partial class Streams : EventStore.Client.Streams.Streams.StreamsBase {
		private readonly ITimeProvider _timeProvider;
		private readonly IQueuedHandler _queue;
		private readonly IReadIndex _readIndex;
		private readonly IAuthenticationProvider _authenticationProvider;
		private readonly int _maxAppendSize;
		private readonly TimeSpan _commitTimeout;

		public Streams(ITimeProvider timeProvider, IQueuedHandler queue, IAuthenticationProvider authenticationProvider, IReadIndex readIndex,
			int maxAppendSize, TimeSpan commitTimeout) {
			if (queue == null) throw new ArgumentNullException(nameof(queue));
			if (authenticationProvider == null) throw new ArgumentNullException(nameof(authenticationProvider));

			_timeProvider = timeProvider;
			_queue = queue;
			_readIndex = readIndex;
			_authenticationProvider = authenticationProvider;
			_maxAppendSize = maxAppendSize;
			_commitTimeout = commitTimeout;
		}
	}
}
