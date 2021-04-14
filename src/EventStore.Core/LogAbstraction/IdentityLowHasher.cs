using EventStore.Core.Index.Hashes;

namespace EventStore.Core.LogAbstraction {
	public class IdentityLowHasher : IHasher<long> {
		public uint Hash(long s) => (uint)s;
	}
}
