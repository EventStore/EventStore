using EventStore.Core.Index.Hashes;
using StreamId = System.UInt32;

namespace EventStore.Core.LogAbstraction {
	public class IdentityLongHasher : ILongHasher<StreamId> {
		public ulong Hash(StreamId s) => s;
	}
}
