using System.Threading.Tasks;
using EventStore.ClientAPI;

namespace EventStore.Core.Tests.ClientAPI.Helpers
{
    internal class StreamWriter
    {
        private readonly EventStoreConnection _store;
        private readonly string _stream;
        private readonly int _version;

        public StreamWriter(EventStoreConnection store, string stream, int version)
        {
            _store = store;
            _stream = stream;
            _version = version;
        }

        public TailWriter Append(params EventData[] events)
        {
            var appends = new Task[events.Length];

            switch (_version)
            {
                case ExpectedVersion.Any:
                    for (var i = 0; i < events.Length; i++)
                        appends[i] = _store.AppendToStreamAsync(_stream, ExpectedVersion.Any, new[] { events[i] });
                    break;
                case -1:
                    for (var i = 0; i < events.Length; i++)
                        appends[i] = _store.AppendToStreamAsync(_stream, i == 0 ? -1 : i, new[] {events[i]});
                    break;
                default:
                    for (var i = 0; i < events.Length; i++)
                        appends[i] = _store.AppendToStreamAsync(_stream, _version + i, new[] {events[i]});
                    break;
            }

            Task.WaitAll(appends);
            return new TailWriter(_store, _stream);
        }
    }

    internal class TailWriter
    {
        private readonly EventStoreConnection _store;
        private readonly string _stream;

        public TailWriter(EventStoreConnection store, string stream)
        {
            _store = store;
            _stream = stream;
        }

        public TailWriter Then(EventData @event, int expectedVersion)
        {
            _store.AppendToStream(_stream, expectedVersion, new[] {@event});
            return this;
        }
    }

    internal class TransactionalWriter
    {
        private readonly EventStoreConnection _store;
        private readonly string _stream;

        public TransactionalWriter(EventStoreConnection store, string stream)
        {
            _store = store;
            _stream = stream;
        }

        public OngoingTransaction StartTransaction(int expectedVersion)
        {
            return new OngoingTransaction(_store.StartTransaction(_stream, expectedVersion));
        }
    }

    //TODO GFY this should be removed and megrged with the public idea of a transaction.
    internal class OngoingTransaction
    {
        private readonly EventStoreTransaction _transaction;

        public OngoingTransaction(EventStoreTransaction transaction)
        {
            _transaction = transaction;
        }

        public OngoingTransaction Write(params EventData[] events)
        {
            _transaction.Write(events);
            return this;
        }

        public void Commit()
        {
            _transaction.Commit();
        }
    }
}
