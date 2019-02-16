using EventStore.ClientAPI;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.Helpers {
	internal class StreamWriter {
		private readonly IEventStoreConnection _store;
		private readonly string _stream;
		private readonly long _version;

		public StreamWriter(IEventStoreConnection store, string stream, long version) {
			_store = store;
			_stream = stream;
			_version = version;
		}

		public TailWriter Append(params EventData[] events) {
			for (var i = 0; i < events.Length; i++) {
				var expVer = _version == ExpectedVersion.Any ? ExpectedVersion.Any : _version + i;
				var nextExpVer = _store.AppendToStreamAsync(_stream, expVer, new[] {events[i]}).Result
					.NextExpectedVersion;
				if (_version != ExpectedVersion.Any)
					Assert.AreEqual(expVer + 1, nextExpVer);
			}

			return new TailWriter(_store, _stream);
		}
	}

	internal class TailWriter {
		private readonly IEventStoreConnection _store;
		private readonly string _stream;

		public TailWriter(IEventStoreConnection store, string stream) {
			_store = store;
			_stream = stream;
		}

		public TailWriter Then(EventData @event, long expectedVersion) {
			_store.AppendToStreamAsync(_stream, expectedVersion, new[] {@event}).Wait();
			return this;
		}
	}

	internal class TransactionalWriter {
		private readonly IEventStoreConnection _store;
		private readonly string _stream;

		public TransactionalWriter(IEventStoreConnection store, string stream) {
			_store = store;
			_stream = stream;
		}

		public OngoingTransaction StartTransaction(long expectedVersion) {
			return new OngoingTransaction(_store.StartTransactionAsync(_stream, expectedVersion).Result);
		}
	}

	//TODO GFY this should be removed and merged with the public idea of a transaction.
	internal class OngoingTransaction {
		private readonly EventStoreTransaction _transaction;

		public OngoingTransaction(EventStoreTransaction transaction) {
			_transaction = transaction;
		}

		public OngoingTransaction Write(params EventData[] events) {
			_transaction.WriteAsync(events).Wait();
			return this;
		}

		public WriteResult Commit() {
			return _transaction.CommitAsync().Result;
		}
	}
}
