using System;
using System.Threading.Tasks;
using EventStore.ClientAPI.Core;
using EventStore.Core.Bus;
using EventStore.Core.Services.Transport.Tcp;

namespace EventStore.ClientAPI.Embedded
{
    internal class EventStoreEmbeddedConnection : EventStoreConnectionBase
    {
        private readonly IPublisher _publisher;

        public EventStoreEmbeddedConnection(ConnectionSettings settings, string connectionName, IPublisher publisher)
            : base(settings, connectionName)
        {
            _publisher = publisher;
        }

        protected override Lazy<IEventStoreConnectionLogicHandler> CreateHandlerLazy(ConnectionSettings settings)
        {
            return new Lazy<IEventStoreConnectionLogicHandler>(() => new EmbeddedEventStoreLogicHandler(this, settings, _publisher));
        }

        public override Task ConnectAsync()
        {
            var source = new TaskCompletionSource<object>();
            source.SetResult(null);
            return source.Task;
        }
    }
}