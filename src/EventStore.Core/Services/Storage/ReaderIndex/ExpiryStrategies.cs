using System;

namespace EventStore.Core.Services.Storage.ReaderIndex {
	public interface IExpiryStrategy {
		public DateTime? GetExpiry();
	}

	// Generates null expiry. Default expiry of now + ESConsts.ReadRequestTimeout will be in effect.
	public class DefaultExpiryStrategy : IExpiryStrategy {
		public DateTime? GetExpiry() => null;
	}
}
