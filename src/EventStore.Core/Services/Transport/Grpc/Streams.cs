using System;
using System.Threading.Channels;
using System.Threading.Tasks;
using EventStore.Core.Bus;
using EventStore.Core.Services.RequestManager;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Plugins.Authorization;

namespace EventStore.Core.Services.Transport.Grpc {
	public partial class Streams : EventStore.Client.Streams.Streams.StreamsBase {
		private readonly IPublisher _publisher;
		private readonly IReadIndex _readIndex;
		private readonly int _maxAppendSize;
		private readonly IAuthorizationProvider _provider;
		private readonly Channel<AppendRequestToken> _writeTokenChannel;
		private static readonly Operation ReadOperation = new Operation(Plugins.Authorization.Operations.Streams.Read);
		private static readonly Operation WriteOperation = new Operation(Plugins.Authorization.Operations.Streams.Write);
		private static readonly Operation DeleteOperation = new Operation(Plugins.Authorization.Operations.Streams.Delete);
		public Streams(
			IPublisher publisher, 
			IReadIndex readIndex,
			int maxAppendSize, 
			IAuthorizationProvider provider
			) {
			if (publisher == null) throw new ArgumentNullException(nameof(publisher));
			_publisher = publisher;
			_readIndex = readIndex;
			_maxAppendSize = maxAppendSize;
			_provider = provider;
			_writeTokenChannel = Channel.CreateBounded<AppendRequestToken>(50);
			for (int i = 0; i < 100; i++) {
				_writeTokenChannel.Writer.TryWrite(new AppendRequestToken());
			}
		}
		public class AppendRequestToken { }
	}
}
