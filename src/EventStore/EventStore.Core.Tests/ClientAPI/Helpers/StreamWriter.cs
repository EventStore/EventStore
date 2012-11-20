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

        public TailWriter Append(params TestEvent[] events)
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

        public TailWriter Then(TestEvent @event, int expectedVersion)
        {
            _store.AppendToStream(_stream, expectedVersion, new[] {@event});
            return this;
        }
    }
}
