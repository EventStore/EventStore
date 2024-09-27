using EventStore.Core.Index.Hashes;

namespace EventStore.Core.LogAbstraction {
	// if the streams are only 32 bit, neednt have 64 bits in the index
	public class IdentityHighHasher : IHasher<uint> {
		public uint Hash(uint s) => 0;
	}
}
