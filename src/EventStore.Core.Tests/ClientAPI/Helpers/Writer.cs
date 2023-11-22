extern alias GrpcClient;
using System.Threading.Tasks;
using GrpcClient::EventStore.Client;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI.Helpers {
	internal class StreamWriter {
		private readonly IEventStoreClient _store;
		private readonly string _stream;
		private readonly long _version;

		public StreamWriter(IEventStoreClient store, string stream, long version) {
			_store = store;
			_stream = stream;
			_version = version;
		}

		public async Task<TailWriter> Append(params EventData[] events) {
			for (var i = 0; i < events.Length; i++) {
				var expVer = _version == ExpectedVersion.Any ? ExpectedVersion.Any : _version + i;
				var nextExpVer = (await _store.AppendToStreamAsync(_stream, expVer, new[] { events[i] })).NextExpectedVersion;
				if (_version != ExpectedVersion.Any)
					Assert.AreEqual(expVer + 1, nextExpVer);
			}

			return new TailWriter(_store, _stream);
		}
	}

	internal class TailWriter {
		private readonly IEventStoreClient _store;
		private readonly string _stream;

		public TailWriter(IEventStoreClient store, string stream) {
			_store = store;
			_stream = stream;
		}

		public async Task<TailWriter> Then(EventData @event, long expectedVersion) {
			await _store.AppendToStreamAsync(_stream, expectedVersion, new[] { @event });
			return this;
		}
	}
}
