using System;
using System.Threading.Channels;
using EventStore.Core.Bus;
using EventStore.Core.Data;
using EventStore.Core.Messages;
using EventStore.Core.Services.RequestManager;
using EventStore.Core.Services.Storage.ReaderIndex;
using EventStore.Plugins.Authorization;

namespace EventStore.Core.Services.Transport.Grpc {
	public partial class Streams : EventStore.Client.Streams.Streams.StreamsBase,		
		IHandle<SystemMessage.StateChangeMessage> {
		private readonly IPublisher _mainQueue;
		private readonly IPublisher _writeQueue;
		private readonly ISubscriber _mainBus;
		private readonly IReadIndex _readIndex;
		private readonly int _maxAppendSize;
		private readonly CommitLevel _commitLevel;
		private readonly IAuthorizationProvider _provider;
		private VNodeState _vnodeState = VNodeState.Initializing;
		private static readonly Operation ReadOperation = new Operation(Plugins.Authorization.Operations.Streams.Read);
		private static readonly Operation WriteOperation = new Operation(Plugins.Authorization.Operations.Streams.Write);
		private static readonly Operation DeleteOperation = new Operation(Plugins.Authorization.Operations.Streams.Delete);
		public Streams(
			IPublisher mainQueue,
			IPublisher writeQueue,
			ISubscriber mainBus,
			IReadIndex readIndex,
			int maxAppendSize, 
			CommitLevel commitLevel,
			IAuthorizationProvider provider) {
			if (mainQueue == null)
				throw new ArgumentNullException(nameof(mainQueue));
			_mainQueue = mainQueue;
			_writeQueue = writeQueue;
			_mainBus = mainBus;
			_readIndex = readIndex;
			_maxAppendSize = maxAppendSize;
			_commitLevel = commitLevel;
			_provider = provider;			
			mainBus.Subscribe<SystemMessage.StateChangeMessage>(this);
		}
		
		public void Handle(SystemMessage.StateChangeMessage message) {			
			_vnodeState = message.State;
		}
		protected void PublishWrite(ClientMessage.WriteEvents @event) {
			if (_vnodeState == VNodeState.Leader) {
				_writeQueue.Publish(@event);
			} else {
				_mainQueue.Publish(@event);
			}
		}
	}

}
