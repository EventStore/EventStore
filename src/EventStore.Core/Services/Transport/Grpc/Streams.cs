using System;
using System.Threading.Channels;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Plugins.Authorization;

namespace EventStore.Core.Services.Transport.Grpc {
	public partial class Streams : EventStore.Client.Streams.Streams.StreamsBase {
		private readonly IPublisher _mainQueue;
		private readonly IPublisher _writeQueue;
		private readonly IReadIndex _readIndex;
		private readonly int _maxAppendSize;
		private readonly IAuthorizationProvider _provider;		
		private static readonly Operation ReadOperation = new Operation(Plugins.Authorization.Operations.Streams.Read);
		private static readonly Operation WriteOperation = new Operation(Plugins.Authorization.Operations.Streams.Write);
		private static readonly Operation DeleteOperation = new Operation(Plugins.Authorization.Operations.Streams.Delete);
		public Streams(
			IPublisher mainQueue,
			IPublisher writeQueue,
			IReadIndex readIndex,
			int maxAppendSize, IAuthorizationProvider provider, int maxWriteConcurrency) {
			if (mainQueue == null) throw new ArgumentNullException(nameof(mainQueue));
			_mainQueue = mainQueue;
			_writeQueue = writeQueue;
			_readIndex = readIndex;
			_maxAppendSize = maxAppendSize;
			_provider = provider;			
		}
	}
	public class AppendRequestToken { }
}
