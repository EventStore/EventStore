using EventStore.Core.Index.Hashes;
using EventStore.Core.Services;

namespace EventStore.Core.Tests.Index.Hashers {
	// Generates hashes that are obvious to humans based on the stream name.
	// The first character of the stream name is the basis of the hash for the corresponding metastream
	// The second character of the stream name is the basis of the hash for the original stream
	// e.g.
	//   "$$ma-1 -> 'm'
	//   "ma-1" -> 'a' (97)
	public class HumanReadableHasher : ILongHasher<string> {
		private readonly HumanReadableHasher32 _hash32;

		public HumanReadableHasher() {
			_hash32 = new HumanReadableHasher32();
		}

		public ulong Hash(string x) => _hash32.Hash(x);
	}

	public class HumanReadableHasher32 : IHasher, IHasher<string> {
		public uint Hash(string x) {
			if (x == "")
				return 0;

			var c = SystemStreams.IsMetastream(x)
				? x[2]
				: x[1];

			return c;
		}

		public uint Hash(byte[] data) {
			throw new System.NotImplementedException();
		}

		public uint Hash(byte[] data, int offset, uint len, uint seed) {
			throw new System.NotImplementedException();
		}
	}
}
