using EventStore.Core.Index.Hashes;
using StreamId = System.UInt32;

namespace EventStore.Core.LogAbstraction {
	public class IdentityLowHasher : IHasher<StreamId> {
		public uint Hash(StreamId s) => s;
	}
}
