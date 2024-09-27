using System;
using EventStore.Core.Services.Storage.ReaderIndex;

namespace EventStore.Core.Tests.Services.Transport.Grpc.StreamsTests {
	public class LotsOfExpiriesStrategy : IExpiryStrategy {
		private int _counter;

		public DateTime? GetExpiry() {
			_counter++;
			if (_counter % 10 == 0) {
				// ok
				return null;
			} else {
				// expired already
				return DateTime.UtcNow - TimeSpan.FromSeconds(1);
			}
		}
	}
}
