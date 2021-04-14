using System;
using EventStore.Core.Index.Hashes;

namespace EventStore.Core.LogAbstraction {
	public class IdentityHighHasher : IHasher<long> {
		public uint Hash(long s) => (uint)(s >> 32);
	}
}
