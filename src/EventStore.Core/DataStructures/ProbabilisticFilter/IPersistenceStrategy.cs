using System;

namespace EventStore.Core.DataStructures.ProbabilisticFilter {
	public interface IPersistenceStrategy : IDisposable {
		BloomFilterAccessor DataAccessor { get; }
		bool Create { get; }
		void Init();
		void Flush();
		Header ReadHeader();
		void WriteHeader(Header header);
	}
}
