using System;
using EventStore.Core.Index.Hashes;

namespace EventStore.Core.Tests.Services.Storage {
	public class ByLengthHasher : IHasher {
		public uint Hash(string s) {
			return (uint)s.Length;
		}

		public uint Hash(byte[] data) {
			return (uint)data.Length;
		}

		public uint Hash(byte[] data, int offset, uint len, uint seed) {
			return len;
		}
	}
}
