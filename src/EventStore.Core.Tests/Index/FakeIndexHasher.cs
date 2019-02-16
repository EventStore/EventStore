using EventStore.Core.Index.Hashes;
using System;

namespace EventStore.Core.Tests.Index {
	public class FakeIndexHasher : IHasher {
		public uint Hash(byte[] data) {
			return (uint)data.Length;
		}

		public uint Hash(string s) {
			return uint.Parse(s);
		}

		public uint Hash(byte[] data, int offset, uint len, uint seed) {
			return (uint)data.Length;
		}
	}
}
